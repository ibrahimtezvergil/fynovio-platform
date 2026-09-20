using Access.Domain.Authentication;
using Access.Domain.Identity;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Application.Authentication;

/// <summary>Everything a freshly established session needs, before it is saved.</summary>
internal sealed record IssuedSession(
    AuthSession Session,
    RefreshToken RefreshToken,
    string RefreshSecret,
    IReadOnlyList<long> MembershipTenantIds,
    long? SelectedTenantId,
    AuthenticationStatus Status)
{
    public string RefreshCookie => $"{RefreshToken.Id}.{RefreshSecret}";
}

/// <summary>The one place a session (+ its first refresh token) is created — used by login and by
/// invitation acceptance so both establish sessions identically. It ADDS the entities to the
/// context but does not save; the caller owns the transaction and the SaveChanges.</summary>
internal sealed class SessionIssuer(SessionOptions options)
{
    public async Task<IssuedSession> PrepareAsync(
        AccessDbContext context,
        long accountId,
        DateTimeOffset now,
        string? userAgentHash,
        CancellationToken cancellationToken)
    {
        // Sets the membership_self_view GUC: this MUST run inside the caller's transaction.
        await context.SetAccountContextAsync(accountId, cancellationToken);

        var session = AuthSession.Create(accountId, now, now.AddDays(options.RefreshAbsoluteDays), userAgentHash);
        context.AuthSessions.Add(session);

        var (secret, hash) = TokenSecrets.GenerateAndHash();
        var refreshToken = RefreshToken.Create(session.Id, hash, now, now.AddDays(options.RefreshIdleDays));
        context.RefreshTokens.Add(refreshToken);

        var memberships = await context.TenantMemberships
            .Where(m => m.AccountId == accountId && m.Status == MembershipStatus.Active)
            .Select(m => m.TenantId.Value)
            .ToListAsync(cancellationToken);

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

        return new IssuedSession(session, refreshToken, secret, memberships, selectedTenantId, status);
    }
}
