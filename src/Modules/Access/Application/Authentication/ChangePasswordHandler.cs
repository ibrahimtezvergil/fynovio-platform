using Access.Domain.Authentication;
using Access.Persistence;
using Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Access.Application.Authentication;

public sealed record ChangePasswordCommand(
    long AccountId,
    Guid CurrentSessionId,
    string CurrentPassword,
    string NewPassword,
    string? CorrelationId = null,
    string? IpHash = null);

public enum ChangePasswordStatus
{
    Completed,
    /// <summary>Wrong current password, locked or unknown account: one answer. Wrong attempts count toward lockout.</summary>
    InvalidCredentials,
    PolicyViolation,
    /// <summary>The credential was changed by someone else while this request ran; nothing was written — retry.</summary>
    Conflict
}

public sealed record ChangePasswordResult(ChangePasswordStatus Status, IReadOnlyList<string>? PolicyViolations = null);

/// <summary>Authenticated password change. The current password is verified with the same rules as login
/// (lockout, failure counting). On success every OTHER session of the account is revoked — the caller's own
/// session stays alive, so changing a password never signs the user out of the tab they are using.</summary>
public sealed class ChangePasswordHandler
{
    private readonly AccessDbContext _context;
    private readonly PasswordService _passwordService;
    private readonly PasswordPolicy _passwordPolicy;
    private readonly CredentialVerifier _verifier;
    private readonly AccountTokenService _tokens;
    private readonly AuthEventWriter _eventWriter;
    private readonly TimeProvider _timeProvider;

    public ChangePasswordHandler(
        AccessDbContext context,
        PasswordService passwordService,
        PasswordPolicy passwordPolicy,
        LockoutOptions lockoutOptions,
        AccountTokenService tokens,
        AuthEventWriter eventWriter,
        TimeProvider? timeProvider = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
        _passwordPolicy = passwordPolicy ?? throw new ArgumentNullException(nameof(passwordPolicy));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _eventWriter = eventWriter ?? throw new ArgumentNullException(nameof(eventWriter));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _verifier = new CredentialVerifier(context, passwordService, lockoutOptions ?? throw new ArgumentNullException(nameof(lockoutOptions)));
    }

    public async Task<ChangePasswordResult> HandleAsync(ChangePasswordCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = _timeProvider.GetUtcNow();
        var credential = await _context.AccountCredentials.FirstOrDefaultAsync(c => c.AccountId == command.AccountId, cancellationToken);
        if (credential is null)
        {
            _passwordService.VerifyDummy(command.CurrentPassword);
            return new ChangePasswordResult(ChangePasswordStatus.InvalidCredentials);
        }

        var check = await _verifier.CheckAsync(credential, command.CurrentPassword, now, cancellationToken);
        if (check is CredentialCheck.Failed or CredentialCheck.Locked)
        {
            await _eventWriter.WriteAsync("password_change", check == CredentialCheck.Locked ? "account_locked" : "bad_password", now,
                credential.AccountId, correlationId: command.CorrelationId, ipHash: command.IpHash,
                detail: new() { ["attemptCount"] = credential.FailedAttempts }, cancellationToken: cancellationToken);
            return new ChangePasswordResult(ChangePasswordStatus.InvalidCredentials);
        }

        // Only a caller who proved the current password gets to learn about policy violations.
        var violations = _passwordPolicy.Validate(command.NewPassword, credential.LoginEmailNormalized).ToList();
        if (!string.IsNullOrEmpty(command.NewPassword)
            && _passwordService.Verify(credential.PasswordHash, command.NewPassword) != PasswordVerificationResult.Failed)
        {
            violations.Add("same_as_current");
        }

        if (violations.Count > 0)
            return new ChangePasswordResult(ChangePasswordStatus.PolicyViolation, violations);

        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            credential.UpdatePasswordHash(_passwordService.HashPassword(command.NewPassword), now);
            await _context.SaveChangesAsync(cancellationToken);

            await _context.AuthSessions
                .Where(s => s.AccountId == command.AccountId && s.Id != command.CurrentSessionId && s.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now).SetProperty(x => x.RevokedReason, "password_changed"), cancellationToken);
            await _tokens.RevokeOutstandingForAccountAsync(command.AccountId, AccountTokenPurpose.PasswordReset, now, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ChangePasswordResult(ChangePasswordStatus.Conflict);
        }

        await _eventWriter.WriteAsync("password_change", "success", now, command.AccountId, sessionId: command.CurrentSessionId,
            correlationId: command.CorrelationId, ipHash: command.IpHash, cancellationToken: cancellationToken);

        return new ChangePasswordResult(ChangePasswordStatus.Completed);
    }
}
