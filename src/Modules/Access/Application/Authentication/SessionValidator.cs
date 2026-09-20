using Access.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Access.Application.Authentication;

/// <summary>Validates whether a session is still active (not revoked, not expired).
/// Called by Host middleware on every protected API request before authorization checks.</summary>
public sealed class SessionValidator
{
    private readonly AccessDbContext _context;
    private readonly TimeProvider _timeProvider;

    public SessionValidator(AccessDbContext context, TimeProvider? timeProvider = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Check if a session is active (not revoked, not expired).</summary>
    public async Task<bool> IsActiveAsync(Guid sessionId, long accountId, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();

        var session = await _context.AuthSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.AccountId == accountId, cancellationToken);

        if (session is null)
            return false;

        return !session.IsRevoked && !session.IsExpired(now);
    }
}
