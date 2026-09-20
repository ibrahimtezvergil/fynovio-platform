using Contracts;

namespace Access.Domain.Authentication;

/// <summary>
/// Represents an active authentication session (sign-in session with a refresh token family).
/// Platform-global, not tenant-scoped — created at login before tenant is selected.
/// </summary>
public sealed class AuthSession
{
    public Guid Id { get; private set; }
    public long AccountId { get; private set; }
    public TenantId? ActiveTenantId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastUsedAt { get; private set; }
    public DateTimeOffset AbsoluteExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevokedReason { get; private set; }
    public string? UserAgentHash { get; private set; }

    private AuthSession() { }

    public static AuthSession Create(
        long accountId,
        DateTimeOffset createdAt,
        DateTimeOffset absoluteExpiresAt,
        string? userAgentHash = null)
    {
        if (accountId <= 0)
            throw new ArgumentException("Account ID must be positive.", nameof(accountId));
        if (absoluteExpiresAt <= createdAt)
            throw new ArgumentException("Absolute expiry must be after creation time.", nameof(absoluteExpiresAt));

        return new AuthSession
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            CreatedAt = createdAt,
            LastUsedAt = createdAt,
            AbsoluteExpiresAt = absoluteExpiresAt,
            UserAgentHash = userAgentHash
        };
    }

    public void SelectTenant(TenantId tenantId)
    {
        if (RevokedAt.HasValue)
            throw new InvalidOperationException("Cannot select tenant on revoked session.");

        ActiveTenantId = tenantId;
    }

    public void ClearTenant()
    {
        ActiveTenantId = null;
    }

    public void UpdateLastUsed(DateTimeOffset now)
    {
        if (RevokedAt.HasValue)
            throw new InvalidOperationException("Cannot update revoked session.");

        LastUsedAt = now;
    }

    public void Revoke(string reason, DateTimeOffset now)
    {
        if (RevokedAt.HasValue)
            throw new InvalidOperationException("Session is already revoked.");

        RevokedAt = now;
        RevokedReason = reason;
    }

    public bool IsExpired(DateTimeOffset now)
    {
        return now >= AbsoluteExpiresAt;
    }

    public bool IsRevoked => RevokedAt.HasValue;
}
