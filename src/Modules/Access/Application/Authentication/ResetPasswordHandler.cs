using Access.Domain.Authentication;
using Access.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Access.Application.Authentication;

/// <summary>Redeems a `password_reset` token (replaces the password) or a `password_setup` token (sets the
/// very first password of an account that has none — the production bootstrap path). The policy is checked
/// BEFORE the token is consumed, so a weak password can be corrected without asking for a new mail. In one
/// transaction: consume (atomic single use) → write the hash (clears the lockout) → revoke every session of
/// the account (a stolen session must not survive a password reset) → revoke other outstanding reset tokens.</summary>
public sealed class ResetPasswordHandler
{
    private readonly AccessDbContext _context;
    private readonly PasswordService _passwordService;
    private readonly PasswordPolicy _passwordPolicy;
    private readonly AccountTokenService _tokens;
    private readonly AuthEventWriter _eventWriter;
    private readonly TimeProvider _timeProvider;

    public ResetPasswordHandler(
        AccessDbContext context,
        PasswordService passwordService,
        PasswordPolicy passwordPolicy,
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
    }

    public async Task<ResetPasswordResult> HandleAsync(ResetPasswordCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = _timeProvider.GetUtcNow();
        var invalid = new ResetPasswordResult(ResetPasswordStatus.InvalidOrExpiredToken);

        var token = await _tokens.FindOutstandingAsync(
            command.Token, [AccountTokenPurpose.PasswordReset, AccountTokenPurpose.PasswordSetup], now, cancellationToken);
        if (token is null)
            return invalid;

        var accountId = token.AccountId!.Value;
        var isSetup = token.Purpose == AccountTokenPurpose.PasswordSetup;
        var credential = await _context.AccountCredentials.FirstOrDefaultAsync(c => c.AccountId == accountId, cancellationToken);
        var account = await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

        // The token's purpose must fit the account: a reset needs a password to replace, a setup needs none to exist.
        if (account is null || (isSetup ? credential is not null : credential is null))
            return invalid;

        var loginEmail = credential?.LoginEmailNormalized ?? EmailNormalizer.Normalize(account.Email);
        var violations = _passwordPolicy.Validate(command.NewPassword, loginEmail);
        if (violations.Count > 0)
            return new ResetPasswordResult(ResetPasswordStatus.PolicyViolation, violations);

        var hash = _passwordService.HashPassword(command.NewPassword);

        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            if (!await _tokens.TryConsumeAsync(token.Id, now, cancellationToken))
                return invalid; // someone else redeemed it first

            if (isSetup)
            {
                _context.AccountCredentials.Add(AccountCredential.Create(accountId, loginEmail, hash));
                await _context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                await WriteHashAsync(credential!, hash, now, cancellationToken);
            }

            await _context.AuthSessions
                .Where(s => s.AccountId == accountId && s.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now).SetProperty(x => x.RevokedReason, "password_reset"), cancellationToken);
            await _tokens.RevokeOutstandingForAccountAsync(accountId, AccountTokenPurpose.PasswordReset, now, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return invalid; // two setup tokens redeemed at once for the same address
        }

        await _eventWriter.WriteAsync(isSetup ? "password_setup_completed" : "password_reset_completed", "success", now, accountId,
            correlationId: command.CorrelationId, ipHash: command.IpHash, cancellationToken: cancellationToken);

        return new ResetPasswordResult(ResetPasswordStatus.Completed);
    }

    /// <summary>A concurrent failed login may have bumped the credential row's version: the reset must still win.</summary>
    private async Task WriteHashAsync(AccountCredential credential, string hash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                credential.UpdatePasswordHash(hash, now);
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < 2)
            {
                await _context.Entry(credential).ReloadAsync(cancellationToken);
            }
        }
    }
}
