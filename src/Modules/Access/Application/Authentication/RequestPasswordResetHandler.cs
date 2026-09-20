using Access.Domain.Authentication;
using Access.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Access.Application.Authentication;

/// <summary>"Forgot password". The response, the work done and the timing class are the same whether or
/// not the address belongs to an account: a known address gets a fresh single-use token by mail (older
/// outstanding ones are revoked), an unknown one gets equivalent dummy work and no mail.</summary>
public sealed class RequestPasswordResetHandler
{
    private readonly AccessDbContext _context;
    private readonly AccountTokenService _tokens;
    private readonly IEmailSender _emailSender;
    private readonly AuthEventWriter _eventWriter;
    private readonly TimeProvider _timeProvider;

    public RequestPasswordResetHandler(
        AccessDbContext context,
        AccountTokenService tokens,
        IEmailSender emailSender,
        AuthEventWriter eventWriter,
        TimeProvider? timeProvider = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
        _eventWriter = eventWriter ?? throw new ArgumentNullException(nameof(eventWriter));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<RequestPasswordResetResult> HandleAsync(RequestPasswordResetCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = _timeProvider.GetUtcNow();
        var accepted = new RequestPasswordResetResult();

        AccountCredential? credential = null;
        if (!string.IsNullOrWhiteSpace(command.Email))
        {
            var normalized = EmailNormalizer.Normalize(command.Email);
            credential = await _context.AccountCredentials.AsNoTracking()
                .FirstOrDefaultAsync(c => c.LoginEmailNormalized == normalized, cancellationToken);
        }

        if (credential is null)
        {
            _ = AccountTokenService.NewSecret(); // the token work an existing account would cost
            await _eventWriter.WriteAsync("password_reset_requested", "accepted", now, correlationId: command.CorrelationId,
                ipHash: command.IpHash, detail: new() { ["known"] = false }, cancellationToken: cancellationToken);
            return accepted;
        }

        var locale = await _context.Accounts.AsNoTracking().Where(a => a.Id == credential.AccountId)
            .Select(a => a.Locale).FirstOrDefaultAsync(cancellationToken);

        await _tokens.RevokeOutstandingForAccountAsync(credential.AccountId, AccountTokenPurpose.PasswordReset, now, cancellationToken);
        var (secret, hash) = AccountTokenService.NewSecret();
        var token = AccountToken.CreatePasswordReset(credential.AccountId, hash, now, _tokens.PasswordResetLifetime);
        _context.AccountTokens.Add(token);
        await _context.SaveChangesAsync(cancellationToken);

        await _eventWriter.WriteAsync("password_reset_requested", "accepted", now, credential.AccountId,
            correlationId: command.CorrelationId, ipHash: command.IpHash, detail: new() { ["known"] = true }, cancellationToken: cancellationToken);

        await _emailSender.SendAsync(
            new EmailMessage(credential.LoginEmailNormalized, EmailMessage.PasswordResetTemplate, locale ?? "tr", new Dictionary<string, string>
            {
                ["token"] = AccountTokenService.Compose(token.Id, secret),
                ["expiresAt"] = token.ExpiresAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture)
            }),
            cancellationToken);

        return accepted;
    }
}
