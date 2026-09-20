using Access.Domain.Authentication;
using Access.Domain.Identity;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Application.Authentication;

/// <summary>Provision a new password account with a credential and platform ExternalIdentity.
/// Idempotent by normalized login email — if an account with that credential already exists,
/// returns its principal. Optionally creates an active membership for a tenant.</summary>
public sealed class ProvisionPasswordAccountHandler
{
    private readonly AccessDbContext _context;
    private readonly PasswordService _passwordService;
    private readonly PasswordPolicy _passwordPolicy;
    private readonly TimeProvider _timeProvider;

    public ProvisionPasswordAccountHandler(
        AccessDbContext context,
        PasswordService passwordService,
        PasswordPolicy passwordPolicy,
        TimeProvider? timeProvider = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
        _passwordPolicy = passwordPolicy ?? throw new ArgumentNullException(nameof(passwordPolicy));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ProvisionPasswordAccountResult> HandleAsync(
        ProvisionPasswordAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null)
            throw new ArgumentNullException(nameof(command));

        // Validate password policy
        var violations = _passwordPolicy.Validate(command.Password, command.Email);
        if (violations.Count > 0)
            throw new InvalidOperationException($"Password policy violation: {string.Join(", ", violations)}");

        var normalizedEmail = EmailNormalizer.Normalize(command.Email);

        // Check if credential already exists
        var existingCredential = await _context.AccountCredentials
            .FirstOrDefaultAsync(c => c.LoginEmailNormalized == normalizedEmail, cancellationToken);

        if (existingCredential is not null)
        {
            // Return existing account's principal
            var existingIdentity = await _context.ExternalIdentities
                .FirstOrDefaultAsync(
                    x => x.Issuer == command.PlatformIssuer && x.AccountId == existingCredential.AccountId,
                    cancellationToken);

            if (existingIdentity is not null)
                return new ProvisionPasswordAccountResult(existingCredential.AccountId, existingIdentity.Principal);

            throw new InvalidOperationException("Account exists but platform identity is missing.");
        }

        // Use transaction to ensure atomicity
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

        // Create account + credential + platform ExternalIdentity
        var (account, principal) = await AccountProvisioning.AddAsync(
            _context,
            command.Email,
            command.DisplayName,
            command.Locale,
            command.PlatformIssuer,
            _passwordService.HashPassword(command.Password),
            normalizedEmail,
            cancellationToken);

        // Optionally create active membership
        if (command.InitialTenantId.HasValue)
        {
            await _context.SetTenantContextAsync(command.InitialTenantId.Value, cancellationToken);
            var membership = TenantMembership.Invite(command.InitialTenantId.Value, account.Id);
            membership.Activate();
            _context.TenantMemberships.Add(membership);
        }

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new ProvisionPasswordAccountResult(account.Id, principal);
    }
}
