using Access.Domain.Authentication;
using Access.Domain.Identity;
using Access.Persistence;
using Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Access.Application.Authentication;

/// <summary>Authenticate a user by email and password. On success, creates an AuthSession,
/// issues a RefreshToken, and loads the account's active memberships. Identical error result
/// for unknown user, bad password, locked account, and no-membership to avoid user enumeration.</summary>
public sealed class AuthenticateHandler
{
    private readonly AccessDbContext _context;
    private readonly PasswordService _passwordService;
    private readonly LockoutOptions _lockoutOptions;
    private readonly SessionOptions _sessionOptions;
    private readonly AuthEventWriter _eventWriter;
    private readonly TimeProvider _timeProvider;

    public AuthenticateHandler(
        AccessDbContext context,
        PasswordService passwordService,
        LockoutOptions lockoutOptions,
        SessionOptions sessionOptions,
        AuthEventWriter eventWriter,
        TimeProvider? timeProvider = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
        _lockoutOptions = lockoutOptions ?? throw new ArgumentNullException(nameof(lockoutOptions));
        _sessionOptions = sessionOptions ?? throw new ArgumentNullException(nameof(sessionOptions));
        _eventWriter = eventWriter ?? throw new ArgumentNullException(nameof(eventWriter));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<AuthenticateResult> HandleAsync(
        AuthenticateCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null)
            throw new ArgumentNullException(nameof(command));

        var normalizedEmail = EmailNormalizer.Normalize(command.Email);
        var now = _timeProvider.GetUtcNow();

        // Lookup credential
        var credential = await _context.AccountCredentials
            .FirstOrDefaultAsync(c => c.LoginEmailNormalized == normalizedEmail, cancellationToken);

        // Determine if we should verify a real password or a dummy
        if (credential is null)
        {
            // Unknown user: cost the same time
            _passwordService.VerifyDummy(command.Password);
            await _eventWriter.WriteAsync(
                "login_failed",
                "unknown_user",
                now,
                correlationId: command.CorrelationId,
                cancellationToken: cancellationToken);
            return new AuthenticateResult(AuthenticationStatus.InvalidCredentials);
        }

        // Check lockout
        if (credential.IsLocked(now))
        {
            _passwordService.VerifyDummy(command.Password);
            await _eventWriter.WriteAsync(
                "login_failed",
                "account_locked",
                now,
                credential.AccountId,
                correlationId: command.CorrelationId,
                cancellationToken: cancellationToken);
            return new AuthenticateResult(AuthenticationStatus.InvalidCredentials);
        }

        // Verify password
        var result = _passwordService.Verify(credential.PasswordHash, command.Password);

        if (result == PasswordVerificationResult.Failed)
        {
            credential.RecordFailedAttempt(_lockoutOptions.MaxFailedAttempts, _lockoutOptions.LockoutMinutes, now);
            await _context.SaveChangesAsync(cancellationToken);

            var outcome = credential.IsLocked(now) ? "account_locked" : "bad_password";
            await _eventWriter.WriteAsync(
                "login_failed",
                outcome,
                now,
                credential.AccountId,
                detail: new() { { "attemptCount", credential.FailedAttempts } },
                correlationId: command.CorrelationId,
                cancellationToken: cancellationToken);

            return new AuthenticateResult(AuthenticationStatus.InvalidCredentials);
        }

        // Success: reset lockout and record login
        credential.ResetFailedAttempts();
        credential.RecordLogin(now);
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            credential.UpdatePasswordHash(_passwordService.HashPassword(command.Password), now);
        }

        // Load account and external identity
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == credential.AccountId, cancellationToken);
        if (account is null)
            return new AuthenticateResult(AuthenticationStatus.InvalidCredentials);

        var externalIdentity = await _context.ExternalIdentities
            .FirstOrDefaultAsync(
                x => x.AccountId == account.Id && x.Issuer == _sessionOptions.PlatformIssuer,
                cancellationToken);

        if (externalIdentity is null)
        {
            // Account exists but no platform identity — should not happen in normal flow
            return new AuthenticateResult(AuthenticationStatus.InvalidCredentials);
        }

        // Use transaction for session creation and membership lookup
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

        // Set account context for membership RLS policy
        await _context.SetAccountContextAsync(account.Id, cancellationToken);

        // Create session
        var sessionCreatedAt = now;
        var sessionAbsoluteExpiry = sessionCreatedAt.AddDays(_sessionOptions.RefreshAbsoluteDays);

        var session = AuthSession.Create(account.Id, sessionCreatedAt, sessionAbsoluteExpiry);
        _context.AuthSessions.Add(session);

        // Create refresh token
        var (tokenSecret, tokenHash) = TokenSecrets.GenerateAndHash();
        var refreshToken = RefreshToken.Create(
            session.Id,
            tokenHash,
            sessionCreatedAt,
            sessionCreatedAt.AddDays(_sessionOptions.RefreshIdleDays));
        _context.RefreshTokens.Add(refreshToken);

        // Load active memberships (uses membership_self_view RLS policy via SetAccountContextAsync)
        var memberships = await _context.TenantMemberships
            .Where(m => m.AccountId == account.Id && m.Status == MembershipStatus.Active)
            .Select(m => m.TenantId.Value)
            .ToListAsync(cancellationToken);

        // Determine response and update session if needed
        long? selectedTenantId = null;
        var status = AuthenticationStatus.NoMembership;

        if (memberships.Count == 1)
        {
            selectedTenantId = memberships[0];
            session.SelectTenant(new TenantId(selectedTenantId.Value));
            status = AuthenticationStatus.Authenticated;
        }
        else if (memberships.Count > 1)
        {
            status = AuthenticationStatus.TenantSelectionRequired;
        }

        // Save all changes atomically
        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        // Write event (outside transaction)
        await _eventWriter.WriteAsync(
            "login_succeeded",
            status.ToString().ToLowerInvariant(),
            now,
            account.Id,
            sessionId: session.Id,
            correlationId: command.CorrelationId,
            cancellationToken: cancellationToken);

        var refreshCookieValue = $"{refreshToken.Id}.{tokenSecret}";

        return new AuthenticateResult(
            status,
            account.Id,
            account.DisplayName,
            memberships,
            selectedTenantId,
            null, // AccessToken will be issued by Host
            refreshCookieValue,
            session.Id,
            externalIdentity.Principal);
    }
}
