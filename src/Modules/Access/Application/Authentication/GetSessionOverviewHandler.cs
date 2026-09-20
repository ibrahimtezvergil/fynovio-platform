using Access.Domain.Identity;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Application.Authentication;

public sealed record SessionOverview(
    long AccountId,
    string DisplayName,
    long? SelectedTenantId,
    IReadOnlyList<long> MembershipTenantIds);

/// <summary>Get the current account's session overview (used by GET /auth/me).
/// Loads the account, active memberships, and capabilities from the session.</summary>
public sealed class GetSessionOverviewHandler
{
    private readonly AccessDbContext _context;
    private readonly TimeProvider _timeProvider;

    public GetSessionOverviewHandler(AccessDbContext context, TimeProvider? timeProvider = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<SessionOverview?> HandleAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();

        var session = await _context.AuthSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session is null || session.IsRevoked || session.IsExpired(now))
            return null;

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == session.AccountId, cancellationToken);

        if (account is null)
            return null;

        // Load active memberships via account context
        await _context.SetAccountContextAsync(account.Id, CancellationToken.None);

        var memberships = await _context.TenantMemberships
            .Where(m => m.AccountId == account.Id && m.Status == MembershipStatus.Active)
            .Select(m => m.TenantId.Value)
            .ToListAsync(cancellationToken);

        return new SessionOverview(
            account.Id,
            account.DisplayName,
            session.ActiveTenantId?.Value,
            memberships);
    }
}
