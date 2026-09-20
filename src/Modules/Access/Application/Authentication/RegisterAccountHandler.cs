using Access.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Access.Application.Authentication;

public sealed record RegisterAccountCommand(
    string Email,
    string DisplayName,
    string Password,
    string? Locale = null,
    string? CorrelationId = null,
    string? IpHash = null);

public enum RegisterAccountStatus
{
    /// <summary>Identical for a new address and for one that already has an account (nothing observable changes).</summary>
    Accepted,
    InvalidEmail,
    InvalidDisplayName,
    PolicyViolation
}

public sealed record RegisterAccountResult(RegisterAccountStatus Status, IReadOnlyList<string>? PolicyViolations = null);

/// <summary>Self-registration — only reachable when the Host enables it. It creates an identity and NOTHING
/// else: no membership, no role, no tenant, no session. Tenant access always comes from an invitation or the
/// bootstrap command, so a self-registered account cannot grant itself anything.</summary>
public sealed class RegisterAccountHandler
{
    private const int MaxDisplayNameLength = 200;

    private readonly AccessDbContext _context;
    private readonly PasswordService _passwordService;
    private readonly PasswordPolicy _passwordPolicy;
    private readonly SessionOptions _sessionOptions;
    private readonly AuthEventWriter _eventWriter;
    private readonly TimeProvider _timeProvider;

    public RegisterAccountHandler(
        AccessDbContext context,
        PasswordService passwordService,
        PasswordPolicy passwordPolicy,
        SessionOptions sessionOptions,
        AuthEventWriter eventWriter,
        TimeProvider? timeProvider = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
        _passwordPolicy = passwordPolicy ?? throw new ArgumentNullException(nameof(passwordPolicy));
        _sessionOptions = sessionOptions ?? throw new ArgumentNullException(nameof(sessionOptions));
        _eventWriter = eventWriter ?? throw new ArgumentNullException(nameof(eventWriter));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<RegisterAccountResult> HandleAsync(RegisterAccountCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!EmailAddressRules.IsPlausible(command.Email))
            return new RegisterAccountResult(RegisterAccountStatus.InvalidEmail);
        if (string.IsNullOrWhiteSpace(command.DisplayName) || command.DisplayName.Trim().Length > MaxDisplayNameLength)
            return new RegisterAccountResult(RegisterAccountStatus.InvalidDisplayName);

        var now = _timeProvider.GetUtcNow();
        var normalized = EmailNormalizer.Normalize(command.Email);

        var violations = _passwordPolicy.Validate(command.Password, normalized);
        if (violations.Count > 0)
            return new RegisterAccountResult(RegisterAccountStatus.PolicyViolation, violations);

        var hash = _passwordService.HashPassword(command.Password); // the same cost for a new and an existing address
        var accepted = new RegisterAccountResult(RegisterAccountStatus.Accepted);

        if (await _context.AccountCredentials.AnyAsync(c => c.LoginEmailNormalized == normalized, cancellationToken))
        {
            await _eventWriter.WriteAsync("registration_created", "accepted", now, correlationId: command.CorrelationId,
                ipHash: command.IpHash, detail: new() { ["known"] = true }, cancellationToken: cancellationToken);
            return accepted;
        }

        long accountId;
        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var (account, _) = await AccountProvisioning.AddAsync(
                _context, normalized, command.DisplayName.Trim(), command.Locale, _sessionOptions.PlatformIssuer, hash, normalized, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            accountId = account.Id;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return accepted; // the same address registered concurrently: indistinguishable from "already exists"
        }

        await _eventWriter.WriteAsync("registration_created", "success", now, accountId, correlationId: command.CorrelationId,
            ipHash: command.IpHash, detail: new() { ["known"] = false }, cancellationToken: cancellationToken);
        return accepted;
    }
}
