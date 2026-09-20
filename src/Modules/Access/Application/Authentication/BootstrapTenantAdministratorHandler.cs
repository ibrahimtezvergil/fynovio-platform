using Access.Domain.Authentication;
using Access.Domain.Identity;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Access.Application.Authentication;

public sealed record BootstrapTenantAdministratorCommand(TenantId TenantId, string Email, string DisplayName, string? CorrelationId = null);

public enum BootstrapTenantAdministratorStatus
{
    Completed,
    AlreadyBootstrapped,
    InvalidEmail
}

/// <summary><see cref="SetupToken"/> is the RAW one-time token — returned once, to the operator who runs the command;
/// nothing else ever sees it (only its hash is stored).</summary>
public sealed record BootstrapTenantAdministratorResult(
    BootstrapTenantAdministratorStatus Status,
    string? SetupToken = null,
    long? AccountId = null,
    PrincipalRef? Principal = null);

/// <summary>The production path to a tenant's first administrator — called ONLY by the operator command, never over
/// HTTP. There is no default credential: the account starts without a password and receives a single-use
/// `password_setup` token (TTL configurable) that the operator hands over. After that, onboarding is by invitation.
/// It refuses to run for a tenant that already has an access state.</summary>
public sealed class BootstrapTenantAdministratorHandler
{
    private readonly AccessDbContext _context;
    private readonly BootstrapTenantAccessHandler _bootstrapAccess;
    private readonly SessionOptions _sessionOptions;
    private readonly AccountTokenService _tokens;
    private readonly AuthEventWriter _eventWriter;
    private readonly TimeProvider _timeProvider;

    public BootstrapTenantAdministratorHandler(
        AccessDbContext context,
        BootstrapTenantAccessHandler bootstrapAccess,
        SessionOptions sessionOptions,
        AccountTokenService tokens,
        AuthEventWriter eventWriter,
        TimeProvider? timeProvider = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _bootstrapAccess = bootstrapAccess ?? throw new ArgumentNullException(nameof(bootstrapAccess));
        _sessionOptions = sessionOptions ?? throw new ArgumentNullException(nameof(sessionOptions));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _eventWriter = eventWriter ?? throw new ArgumentNullException(nameof(eventWriter));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<BootstrapTenantAdministratorResult> HandleAsync(BootstrapTenantAdministratorCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!EmailAddressRules.IsPlausible(command.Email) || string.IsNullOrWhiteSpace(command.DisplayName))
            return new BootstrapTenantAdministratorResult(BootstrapTenantAdministratorStatus.InvalidEmail);

        var now = _timeProvider.GetUtcNow();
        var email = EmailNormalizer.Normalize(command.Email);

        Account account;
        PrincipalRef principal;
        await using (var transaction = await _context.Database.BeginTransactionAsync(cancellationToken))
        {
            await _context.SetTenantContextAsync(command.TenantId, cancellationToken);
            if (await _context.TenantAccessStates.AnyAsync(s => s.TenantId == command.TenantId, cancellationToken))
                return new BootstrapTenantAdministratorResult(BootstrapTenantAdministratorStatus.AlreadyBootstrapped);

            (account, principal) = await AccountProvisioning.AddAsync(
                _context, email, command.DisplayName.Trim(), null, _sessionOptions.PlatformIssuer, passwordHash: null, email, cancellationToken);

            var membership = TenantMembership.Invite(command.TenantId, account.Id);
            membership.Activate();
            _context.TenantMemberships.Add(membership);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        try
        {
            // Roles, grants, access state, evidence and outbox — the existing, separately tested Phase 1.5 handler.
            await _bootstrapAccess.HandleAsync(
                new BootstrapTenantAccessCommand(command.TenantId, principal, CorrelationIds.ParseOrNew(command.CorrelationId)), cancellationToken);
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("already been bootstrapped", StringComparison.OrdinalIgnoreCase))
        {
            return new BootstrapTenantAdministratorResult(BootstrapTenantAdministratorStatus.AlreadyBootstrapped);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two bootstraps of one tenant passed the "already bootstrapped?" check together; the unique
            // (tenant, key) indexes of the tenant-administrator template let exactly one of them through.
            return new BootstrapTenantAdministratorResult(BootstrapTenantAdministratorStatus.AlreadyBootstrapped);
        }

        var (secret, hash) = AccountTokenService.NewSecret();
        var token = AccountToken.CreatePasswordSetup(account.Id, hash, now, _tokens.PasswordSetupLifetime);
        _context.AccountTokens.Add(token);
        await _context.SaveChangesAsync(cancellationToken);

        await _eventWriter.WriteAsync("bootstrap_completed", "success", now, account.Id, command.TenantId.Value,
            correlationId: command.CorrelationId, cancellationToken: cancellationToken);

        return new BootstrapTenantAdministratorResult(
            BootstrapTenantAdministratorStatus.Completed, AccountTokenService.Compose(token.Id, secret), account.Id, principal);
    }
}
