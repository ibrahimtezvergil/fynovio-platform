using Access.Domain.Authentication;
using Xunit;

namespace Access.Tests.Domain.Authentication;

public sealed class AccountCredentialTests
{
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    [Fact]
    public void Create_ValidInputs_CreatesCredential()
    {
        var cred = AccountCredential.Create(1, "TEST@EXAMPLE.COM", "hash123");

        Assert.Equal(1, cred.AccountId);
        Assert.Equal("TEST@EXAMPLE.COM", cred.LoginEmailNormalized);
        Assert.Equal("hash123", cred.PasswordHash);
        Assert.Equal(0, cred.FailedAttempts);
        Assert.Null(cred.LockedUntil);
    }

    [Fact]
    public void RecordFailedAttempt_BelowThreshold_DoesNotLock()
    {
        var cred = AccountCredential.Create(1, "TEST@EXAMPLE.COM", "hash123");

        cred.RecordFailedAttempt(5, 15, _now);
        cred.RecordFailedAttempt(5, 15, _now);

        Assert.Equal(2, cred.FailedAttempts);
        Assert.Null(cred.LockedUntil);
    }

    [Fact]
    public void RecordFailedAttempt_AtThreshold_LocksAccount()
    {
        var cred = AccountCredential.Create(1, "TEST@EXAMPLE.COM", "hash123");

        for (int i = 0; i < 5; i++)
            cred.RecordFailedAttempt(5, 15, _now);

        Assert.Equal(5, cred.FailedAttempts);
        Assert.NotNull(cred.LockedUntil);
        Assert.True(_now.AddMinutes(15) == cred.LockedUntil);
    }

    [Fact]
    public void IsLocked_WithinLockoutWindow_ReturnsTrue()
    {
        var cred = AccountCredential.Create(1, "TEST@EXAMPLE.COM", "hash123");
        cred.RecordFailedAttempt(5, 15, _now);

        var checkTime = _now.AddMinutes(10);
        Assert.True(cred.IsLocked(checkTime));
    }

    [Fact]
    public void IsLocked_AfterLockoutExpiry_ReturnsFalse()
    {
        var cred = AccountCredential.Create(1, "TEST@EXAMPLE.COM", "hash123");
        cred.RecordFailedAttempt(5, 15, _now);

        var checkTime = _now.AddMinutes(20);
        Assert.False(cred.IsLocked(checkTime));
    }

    [Fact]
    public void ResetFailedAttempts_ClearsLockout()
    {
        var cred = AccountCredential.Create(1, "TEST@EXAMPLE.COM", "hash123");
        cred.RecordFailedAttempt(5, 15, _now);
        cred.RecordFailedAttempt(5, 15, _now);

        cred.ResetFailedAttempts();

        Assert.Equal(0, cred.FailedAttempts);
        Assert.Null(cred.LockedUntil);
    }

    [Fact]
    public void UpdatePasswordHash_UpdatesHashAndResetsLockout()
    {
        var cred = AccountCredential.Create(1, "TEST@EXAMPLE.COM", "hash123");
        cred.RecordFailedAttempt(5, 15, _now);

        cred.UpdatePasswordHash("newhash456", _now);

        Assert.Equal("newhash456", cred.PasswordHash);
        Assert.Equal(0, cred.FailedAttempts);
        Assert.Null(cred.LockedUntil);
    }

    [Fact]
    public void RecordLogin_SetsLastLoginAt()
    {
        var cred = AccountCredential.Create(1, "TEST@EXAMPLE.COM", "hash123");

        cred.RecordLogin(_now);

        Assert.Equal(_now, cred.LastLoginAt);
    }
}
