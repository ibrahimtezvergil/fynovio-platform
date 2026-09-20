using Access.Domain.Authentication;
using Access.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Access.Application.Authentication;

public enum ValidateInvitationStatus
{
    Valid,
    /// <summary>The single answer for unknown, malformed, wrong-secret, wrong-purpose, expired, consumed and revoked tokens.</summary>
    InvalidOrExpiredToken
}

public sealed record ValidateInvitationResult(
    ValidateInvitationStatus Status,
    string? MaskedEmail = null,
    long? TenantId = null,
    bool AccountHasCredential = false,
    DateTimeOffset? ExpiresAt = null,
    string? DisplayName = null);

/// <summary>Read-only preview for the accept page. Does not consume the token.</summary>
public sealed class ValidateInvitationHandler
{
    private readonly AccessDbContext _context;
    private readonly AccountTokenService _tokens;
    private readonly TimeProvider _timeProvider;

    public ValidateInvitationHandler(AccessDbContext context, AccountTokenService tokens, TimeProvider? timeProvider = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ValidateInvitationResult> HandleAsync(string? token, CancellationToken cancellationToken = default)
    {
        var found = await _tokens.FindOutstandingAsync(token, [AccountTokenPurpose.Invite], _timeProvider.GetUtcNow(), cancellationToken);
        if (found is null)
            return new ValidateInvitationResult(ValidateInvitationStatus.InvalidOrExpiredToken);

        var email = found.EmailNormalized!;
        var hasCredential = await _context.AccountCredentials.AnyAsync(c => c.LoginEmailNormalized == email, cancellationToken);

        return new ValidateInvitationResult(
            ValidateInvitationStatus.Valid,
            EmailAddressRules.Mask(email),
            found.TenantId,
            hasCredential,
            found.ExpiresAt,
            found.DisplayName);
    }
}
