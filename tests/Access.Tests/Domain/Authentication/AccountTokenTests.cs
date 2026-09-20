using Access.Domain.Authentication;
using Xunit;

namespace Access.Tests.Domain.Authentication;

public sealed class AccountTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Invite_carries_tenant_and_email_and_expires_after_its_lifetime()
    {
        var token = AccountToken.CreateInvite(7, "new@example.com", " New Person ", " tr ", "hash", Now, TimeSpan.FromDays(7), createdByAccountId: 3);

        Assert.Equal(AccountTokenPurpose.Invite, token.Purpose);
        Assert.Equal(7, token.TenantId);
        Assert.Equal("new@example.com", token.EmailNormalized);
        Assert.Equal("New Person", token.DisplayName);
        Assert.Equal("tr", token.Locale);
        Assert.Equal(3, token.CreatedByAccountId);
        Assert.Equal(Now.AddDays(7), token.ExpiresAt);
        Assert.Null(token.AccountId);
    }

    [Fact]
    public void Reset_and_setup_tokens_belong_to_an_account_and_not_to_a_tenant()
    {
        var reset = AccountToken.CreatePasswordReset(5, "hash-1", Now, TimeSpan.FromMinutes(30));
        var setup = AccountToken.CreatePasswordSetup(5, "hash-2", Now, TimeSpan.FromHours(24));

        Assert.Equal(AccountTokenPurpose.PasswordReset, reset.Purpose);
        Assert.Equal(AccountTokenPurpose.PasswordSetup, setup.Purpose);
        Assert.All(new[] { reset, setup }, t =>
        {
            Assert.Equal(5, t.AccountId);
            Assert.Null(t.TenantId);
            Assert.Null(t.EmailNormalized);
        });
    }

    [Fact]
    public void Only_the_hash_is_a_member_of_the_token_and_ids_are_unique()
    {
        var a = AccountToken.CreatePasswordReset(1, "hash-a", Now, TimeSpan.FromMinutes(30));
        var b = AccountToken.CreatePasswordReset(1, "hash-b", Now, TimeSpan.FromMinutes(30));

        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal("hash-a", a.TokenHash);
        Assert.DoesNotContain(typeof(AccountToken).GetProperties(), p => p.Name is "Secret" or "RawToken" or "Token");
    }

    [Fact]
    public void Outstanding_until_expired_consumed_or_revoked()
    {
        var token = AccountToken.CreatePasswordReset(1, "h", Now, TimeSpan.FromMinutes(30));

        Assert.True(token.IsOutstanding(Now));
        Assert.True(token.IsOutstanding(Now.AddMinutes(29)));
        Assert.False(token.IsOutstanding(Now.AddMinutes(30))); // the expiry instant itself is expired
        Assert.False(token.IsOutstanding(Now.AddHours(1)));

        token.Revoke(Now.AddMinutes(1));
        Assert.False(token.IsOutstanding(Now.AddMinutes(2)));
        Assert.Equal(Now.AddMinutes(1), token.RevokedAt);

        token.Revoke(Now.AddMinutes(5)); // idempotent: keeps the first revocation time
        Assert.Equal(Now.AddMinutes(1), token.RevokedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_lifetimes_and_ids_are_rejected(long id)
    {
        Assert.Throws<ArgumentException>(() => AccountToken.CreatePasswordReset(id, "h", Now, TimeSpan.FromMinutes(1)));
        Assert.Throws<ArgumentException>(() => AccountToken.CreateInvite(id, "a@example.com", null, null, "h", Now, TimeSpan.FromMinutes(1), null));
        Assert.Throws<ArgumentException>(() => AccountToken.CreatePasswordReset(1, "h", Now, TimeSpan.FromSeconds(id)));
    }

    [Fact]
    public void Blank_hash_or_email_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => AccountToken.CreatePasswordReset(1, " ", Now, TimeSpan.FromMinutes(1)));
        Assert.Throws<ArgumentException>(() => AccountToken.CreateInvite(1, " ", null, null, "h", Now, TimeSpan.FromMinutes(1), null));
    }
}
