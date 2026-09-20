namespace Access.Domain.Authentication;

public static class AccountTokenPurpose
{
    public const string Invite = "invite";
    public const string PasswordReset = "password_reset";
    public const string PasswordSetup = "password_setup";
}

/// <summary>
/// Single-purpose, single-use opaque token (invitation, password reset, first-password setup).
/// Only the SHA-256 hash of the secret is stored. Platform-global like <see cref="AccountCredential"/>:
/// it must be readable before any tenant is known, so it is deliberately outside tenant RLS
/// (docs/schema/identity-access-schema.md, Revision 9). Consumption is an atomic compare-and-set
/// performed by the handlers (`UPDATE … WHERE consumed_at IS NULL …`), not by this class.
/// </summary>
public sealed class AccountToken
{
    public Guid Id { get; private set; }
    public string Purpose { get; private set; } = null!;
    public string TokenHash { get; private set; } = null!;
    public long? AccountId { get; private set; }
    public long? TenantId { get; private set; }
    public string? EmailNormalized { get; private set; }
    public string? DisplayName { get; private set; }
    public string? Locale { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public long? CreatedByAccountId { get; private set; }

    private AccountToken() { }

    public static AccountToken CreateInvite(
        long tenantId,
        string emailNormalized,
        string? displayName,
        string? locale,
        string tokenHash,
        DateTimeOffset now,
        TimeSpan lifetime,
        long? createdByAccountId)
    {
        if (tenantId <= 0)
            throw new ArgumentException("Tenant ID must be positive.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(emailNormalized))
            throw new ArgumentException("Email is required.", nameof(emailNormalized));

        var token = Create(AccountTokenPurpose.Invite, tokenHash, now, lifetime);
        token.TenantId = tenantId;
        token.EmailNormalized = emailNormalized;
        token.DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        token.Locale = string.IsNullOrWhiteSpace(locale) ? null : locale.Trim();
        token.CreatedByAccountId = createdByAccountId;
        return token;
    }

    public static AccountToken CreatePasswordReset(long accountId, string tokenHash, DateTimeOffset now, TimeSpan lifetime) =>
        CreateForAccount(AccountTokenPurpose.PasswordReset, accountId, tokenHash, now, lifetime);

    public static AccountToken CreatePasswordSetup(long accountId, string tokenHash, DateTimeOffset now, TimeSpan lifetime) =>
        CreateForAccount(AccountTokenPurpose.PasswordSetup, accountId, tokenHash, now, lifetime);

    private static AccountToken CreateForAccount(string purpose, long accountId, string tokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        if (accountId <= 0)
            throw new ArgumentException("Account ID must be positive.", nameof(accountId));

        var token = Create(purpose, tokenHash, now, lifetime);
        token.AccountId = accountId;
        return token;
    }

    private static AccountToken Create(string purpose, string tokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        if (lifetime <= TimeSpan.Zero)
            throw new ArgumentException("Lifetime must be positive.", nameof(lifetime));

        return new AccountToken
        {
            Id = Guid.NewGuid(),
            Purpose = purpose,
            TokenHash = tokenHash,
            ExpiresAt = now.Add(lifetime),
            CreatedAt = now
        };
    }

    public bool IsOutstanding(DateTimeOffset now) => ConsumedAt is null && RevokedAt is null && now < ExpiresAt;

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
