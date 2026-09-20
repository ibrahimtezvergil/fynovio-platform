using Contracts;

namespace Access.Domain.Authentication;

/// <summary>
/// Password credential for an account. Platform-global, not tenant-scoped — login happens
/// before tenant is known. Uniqueness of the sign-in handle is enforced via
/// <see cref="LoginEmailNormalized"/> UNIQUE constraint; <see cref="Access.Domain.Identity.Account"/>.Email
/// is profile data only (docs/schema/identity-access-schema.md).
/// </summary>
public sealed class AccountCredential : IHasRowVersion
{
    public long AccountId { get; private set; }
    public string LoginEmailNormalized { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public DateTimeOffset PasswordChangedAt { get; private set; }
    public int FailedAttempts { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public long RowVersion { get; private set; } = 1;

    private AccountCredential() { }

    public static AccountCredential Create(long accountId, string loginEmailNormalized, string passwordHash)
    {
        if (accountId <= 0)
            throw new ArgumentException("Account ID must be positive.", nameof(accountId));
        if (string.IsNullOrWhiteSpace(loginEmailNormalized))
            throw new ArgumentException("Login email normalized is required.", nameof(loginEmailNormalized));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));

        var now = DateTimeOffset.UtcNow;
        return new AccountCredential
        {
            AccountId = accountId,
            LoginEmailNormalized = loginEmailNormalized,
            PasswordHash = passwordHash,
            PasswordChangedAt = now,
            FailedAttempts = 0,
            LockedUntil = null
        };
    }

    public void RecordFailedAttempt(int maxFailedAttempts, int lockoutMinutes, DateTimeOffset now)
    {
        FailedAttempts++;
        if (FailedAttempts >= maxFailedAttempts)
            LockedUntil = now.AddMinutes(lockoutMinutes);
    }

    public void ResetFailedAttempts()
    {
        FailedAttempts = 0;
        LockedUntil = null;
    }

    public void UpdatePasswordHash(string newHash, DateTimeOffset now)
    {
        PasswordHash = newHash;
        PasswordChangedAt = now;
        ResetFailedAttempts();
    }

    public void RecordLogin(DateTimeOffset now)
    {
        LastLoginAt = now;
    }

    public bool IsLocked(DateTimeOffset now)
    {
        return LockedUntil.HasValue && now < LockedUntil.Value;
    }

    void IHasRowVersion.IncrementRowVersion() => RowVersion++;
}
