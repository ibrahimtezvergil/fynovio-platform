using Microsoft.EntityFrameworkCore;
using Access.Application.Authentication;
using Access.Domain.Authentication;
using Xunit;

namespace Access.Tests.Integration.Authentication;

public sealed class AccountTokenParsingTests
{
    [Fact]
    public void Composed_tokens_round_trip()
    {
        var id = Guid.NewGuid();

        Assert.True(AccountTokenService.TryParse(AccountTokenService.Compose(id, "abc_DEF-123"), out var parsedId, out var secret));
        Assert.Equal(id, parsedId);
        Assert.Equal("abc_DEF-123", secret);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-separator")]
    [InlineData(".secret-without-id")]
    [InlineData("11111111-1111-1111-1111-111111111111.")]
    [InlineData("not-a-guid.secret")]
    [InlineData("11111111-1111-1111-1111-111111111111.a.b")]
    [InlineData("{11111111-1111-1111-1111-111111111111}.secret")]
    public void Malformed_input_is_not_a_token_and_never_throws(string? raw) =>
        Assert.False(AccountTokenService.TryParse(raw, out _, out _));

    [Fact]
    public void Oversized_input_is_not_a_token() =>
        Assert.False(AccountTokenService.TryParse(Guid.NewGuid() + "." + new string('x', 10_000), out _, out _));

    [Fact]
    public void Email_messages_never_print_their_values()
    {
        var message = new EmailMessage("a@example.com", EmailMessage.InviteTemplate, "tr", new Dictionary<string, string> { ["token"] = "SUPER-SECRET-TOKEN" });

        Assert.DoesNotContain("SUPER-SECRET-TOKEN", message.ToString());
        Assert.DoesNotContain("SUPER-SECRET-TOKEN", $"{message}");
    }
}

public sealed class AccountTokenServiceTests : IClassFixture<PostgresFixture>
{
    private static readonly string[] AnyPurpose = [AccountTokenPurpose.Invite, AccountTokenPurpose.PasswordReset, AccountTokenPurpose.PasswordSetup];

    private readonly PostgresFixture _fixture;

    public AccountTokenServiceTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<(string Raw, long AccountId)> ResetTokenAsync(TestTimeProvider time)
    {
        var (accountId, _) = await AuthTestSetup.SeedAccountAsync(_fixture, AuthTestSetup.NewEmail());
        var raw = await AuthTestSetup.InsertTokenAsync(_fixture, hash =>
            AccountToken.CreatePasswordReset(accountId, hash, time.GetUtcNow(), TimeSpan.FromMinutes(30)));
        return (raw, accountId);
    }

    [Fact]
    public async Task A_live_token_is_found_without_being_consumed()
    {
        var time = new TestTimeProvider();
        var (raw, accountId) = await ResetTokenAsync(time);
        await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);

        var found = await AuthTestSetup.Tokens(context).FindOutstandingAsync(raw, AnyPurpose, time.GetUtcNow(), default);

        Assert.NotNull(found);
        Assert.Equal(accountId, found.AccountId);
        Assert.Null((await AuthTestSetup.LoadTokenAsync(_fixture, raw)).ConsumedAt);
    }

    [Fact]
    public async Task Every_invalid_cause_yields_the_same_null()
    {
        var time = new TestTimeProvider();
        var (live, accountId) = await ResetTokenAsync(time);
        var (id, secret) = (Guid.Empty, string.Empty);
        AccountTokenService.TryParse(live, out id, out secret);
        var consumed = await AuthTestSetup.InsertTokenAsync(_fixture, h => AccountToken.CreatePasswordReset(accountId, h, time.GetUtcNow(), TimeSpan.FromMinutes(30)));
        var revoked = await AuthTestSetup.InsertTokenAsync(_fixture, h => AccountToken.CreatePasswordReset(accountId, h, time.GetUtcNow(), TimeSpan.FromMinutes(30)));
        var invite = await AuthTestSetup.InsertTokenAsync(_fixture, h => AccountToken.CreateInvite(1, "x@example.com", null, null, h, time.GetUtcNow(), TimeSpan.FromDays(1), null));

        await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);
        var service = AuthTestSetup.Tokens(context);
        Assert.True(AccountTokenService.TryParse(consumed, out var consumedId, out _));
        Assert.True(AccountTokenService.TryParse(revoked, out var revokedId, out _));
        Assert.True(await service.TryConsumeAsync(consumedId, time.GetUtcNow(), default));
        await context.AccountTokens.Where(t => t.Id == revokedId).ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, time.GetUtcNow()));

        var resetOnly = new[] { AccountTokenPurpose.PasswordReset };
        var causes = new (string Name, string? Raw, string[] Purposes, DateTimeOffset At)[]
        {
            ("consumed", consumed, resetOnly, time.GetUtcNow()),
            ("revoked", revoked, resetOnly, time.GetUtcNow()),
            ("expired", live, resetOnly, time.GetUtcNow().AddMinutes(31)),
            ("wrong secret", $"{id:D}.{secret}x", resetOnly, time.GetUtcNow()),
            ("wrong purpose", invite, resetOnly, time.GetUtcNow()),
            ("purpose not accepted", live, [AccountTokenPurpose.Invite], time.GetUtcNow()),
            ("unknown id", $"{Guid.NewGuid():D}.{secret}", resetOnly, time.GetUtcNow()),
            ("malformed", "garbage", resetOnly, time.GetUtcNow()),
            ("empty", "", resetOnly, time.GetUtcNow()),
        };

        foreach (var cause in causes)
            Assert.True(await service.FindOutstandingAsync(cause.Raw, cause.Purposes, cause.At, default) is null, $"{cause.Name} must not be found");

        Assert.NotNull(await service.FindOutstandingAsync(live, resetOnly, time.GetUtcNow(), default)); // and the live one still is
    }

    [Fact]
    public async Task Consuming_is_single_use_even_when_many_callers_race()
    {
        var time = new TestTimeProvider();
        var (raw, _) = await ResetTokenAsync(time);
        Assert.True(AccountTokenService.TryParse(raw, out var id, out _));

        var attempts = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);
            return await AuthTestSetup.Tokens(context).TryConsumeAsync(id, time.GetUtcNow(), default);
        }));

        Assert.Equal(1, attempts.Count(won => won));
        Assert.NotNull((await AuthTestSetup.LoadTokenAsync(_fixture, raw)).ConsumedAt);
    }

    [Fact]
    public async Task An_expired_token_cannot_be_consumed()
    {
        var time = new TestTimeProvider();
        var (raw, _) = await ResetTokenAsync(time);
        Assert.True(AccountTokenService.TryParse(raw, out var id, out _));
        await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);

        Assert.False(await AuthTestSetup.Tokens(context).TryConsumeAsync(id, time.GetUtcNow().AddMinutes(30), default));
        Assert.Null((await AuthTestSetup.LoadTokenAsync(_fixture, raw)).ConsumedAt);
    }

    [Fact]
    public async Task Outstanding_invites_and_account_tokens_can_be_revoked_in_bulk()
    {
        var time = new TestTimeProvider();
        var tenant = AuthTestSetup.NewTenant().Value;
        var email = AuthTestSetup.NewEmail();
        var first = await AuthTestSetup.InsertTokenAsync(_fixture, h => AccountToken.CreateInvite(tenant, email, null, null, h, time.GetUtcNow(), TimeSpan.FromDays(1), null));
        var otherTenant = await AuthTestSetup.InsertTokenAsync(_fixture, h => AccountToken.CreateInvite(tenant + 1, email, null, null, h, time.GetUtcNow(), TimeSpan.FromDays(1), null));
        var (reset, accountId) = await ResetTokenAsync(time);

        await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);
        var service = AuthTestSetup.Tokens(context);

        Assert.Equal(1, await service.RevokeOutstandingInvitesAsync(tenant, email, time.GetUtcNow(), default));
        Assert.Equal(1, await service.RevokeOutstandingForAccountAsync(accountId, AccountTokenPurpose.PasswordReset, time.GetUtcNow(), default));

        Assert.NotNull((await AuthTestSetup.LoadTokenAsync(_fixture, first)).RevokedAt);
        Assert.Null((await AuthTestSetup.LoadTokenAsync(_fixture, otherTenant)).RevokedAt); // other tenant untouched
        Assert.NotNull((await AuthTestSetup.LoadTokenAsync(_fixture, reset)).RevokedAt);
    }
}
