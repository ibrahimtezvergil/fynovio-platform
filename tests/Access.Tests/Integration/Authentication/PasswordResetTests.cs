using Access.Application.Authentication;
using Access.Domain.Authentication;
using Access.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Access.Tests.Integration.Authentication;

public sealed class PasswordResetTests : IClassFixture<PostgresFixture>
{
    private const string NewPassword = "Another-Passw0rd-2026";

    private readonly PostgresFixture _fixture;

    public PasswordResetTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<RequestPasswordResetResult> ForgotAsync(string email, FakeEmailSender mail, TimeProvider time)
    {
        await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);
        return await AuthTestSetup.Forgot(context, mail, time).HandleAsync(new RequestPasswordResetCommand(email));
    }

    private async Task<ResetPasswordResult> ResetAsync(string token, string password, TimeProvider time)
    {
        await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);
        return await AuthTestSetup.Reset(context, time).HandleAsync(new ResetPasswordCommand(token, password));
    }

    private Task<long> CountAsync(string sql, params (string, object)[] p) => AuthTestSetup.ScalarLongAsync(_fixture, sql, p);

    private static readonly ResetPasswordResult Invalid = new(ResetPasswordStatus.InvalidOrExpiredToken);

    // ---- forgot ----------------------------------------------------------------------------

    [Fact]
    public async Task A_known_address_gets_one_mail_and_a_short_lived_single_use_token()
    {
        var email = AuthTestSetup.NewEmail();
        var (accountId, _) = await AuthTestSetup.SeedAccountAsync(_fixture, email);
        var mail = new FakeEmailSender();
        var time = new TestTimeProvider();

        var result = await ForgotAsync(email.ToUpperInvariant(), mail, time);

        Assert.Equal(new RequestPasswordResetResult(), result);
        var message = Assert.Single(mail.Messages);
        Assert.Equal(EmailMessage.PasswordResetTemplate, message.TemplateId);
        Assert.Equal(EmailNormalizer.Normalize(email), message.To);
        var stored = await AuthTestSetup.LoadTokenAsync(_fixture, message.Values["token"]);
        Assert.Equal(AccountTokenPurpose.PasswordReset, stored.Purpose);
        Assert.Equal(accountId, stored.AccountId);
        Assert.Equal(time.GetUtcNow().AddMinutes(30), stored.ExpiresAt);
        Assert.NotEqual(message.Values["token"][(message.Values["token"].IndexOf('.') + 1)..], stored.TokenHash);
    }

    [Fact]
    public async Task Unknown_malformed_and_blank_addresses_get_the_identical_result_and_no_mail_or_token()
    {
        var (_, _) = await AuthTestSetup.SeedAccountAsync(_fixture, AuthTestSetup.NewEmail());
        var mail = new FakeEmailSender();
        var time = new TestTimeProvider();
        var known = AuthTestSetup.NewEmail();
        await AuthTestSetup.SeedAccountAsync(_fixture, known);
        var before = await CountAsync("SELECT count(*) FROM identity.account_tokens");

        var knownResult = await ForgotAsync(known, mail, time);
        var unknownResults = new[]
        {
            await ForgotAsync(AuthTestSetup.NewEmail(), mail, time),
            await ForgotAsync("not-an-email", mail, time),
            await ForgotAsync("", mail, time),
            await ForgotAsync("   ", mail, time),
        };

        Assert.All(unknownResults, r => Assert.Equal(knownResult, r));
        Assert.Single(mail.Messages); // only the known address received anything
        Assert.Equal(before + 1, await CountAsync("SELECT count(*) FROM identity.account_tokens")); // and only one token was stored
    }

    [Fact]
    public async Task The_audit_event_records_whether_the_address_was_known_but_never_the_address()
    {
        var known = AuthTestSetup.NewEmail();
        var unknown = AuthTestSetup.NewEmail();
        await AuthTestSetup.SeedAccountAsync(_fixture, known);
        var time = new TestTimeProvider();
        var (knownCorrelation, unknownCorrelation) = (Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
        await using (var context = await AuthTestSetup.RuntimeContextAsync(_fixture))
        {
            var handler = AuthTestSetup.Forgot(context, new FakeEmailSender(), time);
            await handler.HandleAsync(new RequestPasswordResetCommand(known, knownCorrelation));
            await handler.HandleAsync(new RequestPasswordResetCommand(unknown, unknownCorrelation));
        }

        await using var admin = _fixture.CreateAdminContext();
        var knownEvent = await admin.AuthEvents.SingleAsync(e => e.CorrelationId == knownCorrelation);
        var unknownEvent = await admin.AuthEvents.SingleAsync(e => e.CorrelationId == unknownCorrelation);
        Assert.True(System.Text.Json.JsonDocument.Parse(knownEvent.Detail!).RootElement.GetProperty("known").GetBoolean());
        Assert.False(System.Text.Json.JsonDocument.Parse(unknownEvent.Detail!).RootElement.GetProperty("known").GetBoolean());
        Assert.NotNull(knownEvent.AccountId);
        Assert.Null(unknownEvent.AccountId);
        Assert.All(new[] { knownEvent, unknownEvent }, e => Assert.DoesNotContain("@", e.Detail + e.Outcome + e.EventType));
    }

    [Fact]
    public async Task Asking_again_revokes_the_earlier_token_so_only_the_newest_works()
    {
        var email = AuthTestSetup.NewEmail();
        await AuthTestSetup.SeedAccountAsync(_fixture, email);
        var mail = new FakeEmailSender();
        var time = new TestTimeProvider();
        await ForgotAsync(email, mail, time);
        await ForgotAsync(email, mail, time);
        var (first, second) = (mail.Messages[0].Values["token"], mail.Messages[1].Values["token"]);

        Assert.Equal(Invalid, await ResetAsync(first, NewPassword, time));
        Assert.Equal(ResetPasswordStatus.Completed, (await ResetAsync(second, NewPassword, time)).Status);
    }

    // ---- reset -----------------------------------------------------------------------------

    [Fact]
    public async Task Resetting_replaces_the_password_and_the_token_cannot_be_replayed()
    {
        var email = AuthTestSetup.NewEmail();
        await AuthTestSetup.SeedAccountAsync(_fixture, email);
        var mail = new FakeEmailSender();
        var time = new TestTimeProvider();
        await ForgotAsync(email, mail, time);
        var token = mail.TokenFor(email);

        var result = await ResetAsync(token, NewPassword, time);

        Assert.Equal(new ResetPasswordResult(ResetPasswordStatus.Completed), result);
        Assert.Equal(AuthenticationStatus.NoMembership, (await AuthTestSetup.LoginAsync(_fixture, email, NewPassword, time)).Status); // signed in (no membership yet)
        Assert.Equal(AuthenticationStatus.InvalidCredentials, (await AuthTestSetup.LoginAsync(_fixture, email, AuthTestSetup.Password, time)).Status);
        Assert.NotNull((await AuthTestSetup.LoadTokenAsync(_fixture, token)).ConsumedAt);
        Assert.Equal(Invalid, await ResetAsync(token, "Yet-Another-Passw0rd-1", time)); // replay
        Assert.Equal(AuthenticationStatus.NoMembership, (await AuthTestSetup.LoginAsync(_fixture, email, NewPassword, time)).Status); // password unchanged by the replay
    }

    [Fact]
    public async Task Resetting_revokes_every_session_of_the_account_and_none_can_refresh_afterwards()
    {
        var email = AuthTestSetup.NewEmail();
        var (accountId, _) = await AuthTestSetup.SeedAccountAsync(_fixture, email);
        var otherAccountEmail = AuthTestSetup.NewEmail();
        await AuthTestSetup.SeedAccountAsync(_fixture, otherAccountEmail);
        var mail = new FakeEmailSender();
        var time = new TestTimeProvider();
        var first = await AuthTestSetup.LoginAsync(_fixture, email, AuthTestSetup.Password, time);
        var second = await AuthTestSetup.LoginAsync(_fixture, email, AuthTestSetup.Password, time);
        var bystander = await AuthTestSetup.LoginAsync(_fixture, otherAccountEmail, AuthTestSetup.Password, time);
        Assert.Equal(RefreshResult.Success, (await AuthTestSetup.RefreshAsync(_fixture, second.RefreshCookie!, time)).Status); // alive before

        await ForgotAsync(email, mail, time);
        await ResetAsync(mail.TokenFor(email), NewPassword, time);

        Assert.Equal(RefreshResult.SessionInvalid, (await AuthTestSetup.RefreshAsync(_fixture, first.RefreshCookie!, time)).Status);
        Assert.Equal(RefreshResult.SessionInvalid, (await AuthTestSetup.RefreshAsync(_fixture, second.RefreshCookie!, time)).Status);
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM identity.auth_sessions WHERE account_id = @a AND revoked_at IS NULL", ("a", accountId)));
        Assert.Equal(2, await CountAsync("SELECT count(*) FROM identity.auth_sessions WHERE account_id = @a AND revoked_reason = 'password_reset'", ("a", accountId)));
        Assert.Equal(RefreshResult.Success, (await AuthTestSetup.RefreshAsync(_fixture, bystander.RefreshCookie!, time)).Status); // other accounts untouched
    }

    [Fact]
    public async Task Resetting_clears_a_lockout()
    {
        var email = AuthTestSetup.NewEmail();
        var (accountId, _) = await AuthTestSetup.SeedAccountAsync(_fixture, email);
        var time = new TestTimeProvider();
        for (var i = 0; i < 5; i++)
            await AuthTestSetup.LoginAsync(_fixture, email, "Wrong-Password-1234", time);
        Assert.NotEqual(0, await CountAsync("SELECT failed_attempts FROM identity.account_credentials WHERE account_id = @a", ("a", accountId)));
        Assert.Equal(AuthenticationStatus.InvalidCredentials, (await AuthTestSetup.LoginAsync(_fixture, email, AuthTestSetup.Password, time)).Status); // locked
        var mail = new FakeEmailSender();

        await ForgotAsync(email, mail, time); // a locked account can still ask
        await ResetAsync(mail.TokenFor(email), NewPassword, time);

        Assert.Equal(0, await CountAsync("SELECT failed_attempts FROM identity.account_credentials WHERE account_id = @a", ("a", accountId)));
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM identity.account_credentials WHERE account_id = @a AND locked_until IS NOT NULL", ("a", accountId)));
        Assert.Equal(AuthenticationStatus.NoMembership, (await AuthTestSetup.LoginAsync(_fixture, email, NewPassword, time)).Status);
    }

    [Fact]
    public async Task A_password_that_breaks_the_policy_does_not_consume_the_token()
    {
        var email = AuthTestSetup.NewEmail();
        await AuthTestSetup.SeedAccountAsync(_fixture, email);
        var mail = new FakeEmailSender();
        var time = new TestTimeProvider();
        await ForgotAsync(email, mail, time);
        var token = mail.TokenFor(email);

        var weak = await ResetAsync(token, "short", time);
        var sameAsEmail = await ResetAsync(token, email, time);

        Assert.Equal(ResetPasswordStatus.PolicyViolation, weak.Status);
        Assert.Contains("too_short", weak.PolicyViolations!);
        Assert.Equal(ResetPasswordStatus.PolicyViolation, sameAsEmail.Status);
        Assert.Contains("equals_email", sameAsEmail.PolicyViolations!);
        Assert.Null((await AuthTestSetup.LoadTokenAsync(_fixture, token)).ConsumedAt);
        Assert.Equal(AuthenticationStatus.NoMembership, (await AuthTestSetup.LoginAsync(_fixture, email, AuthTestSetup.Password, time)).Status); // old password still in force

        Assert.Equal(ResetPasswordStatus.Completed, (await ResetAsync(token, NewPassword, time)).Status);
    }

    [Fact]
    public async Task Concurrent_resets_of_one_token_have_exactly_one_winner()
    {
        var email = AuthTestSetup.NewEmail();
        await AuthTestSetup.SeedAccountAsync(_fixture, email);
        var mail = new FakeEmailSender();
        var time = new TestTimeProvider();
        await ForgotAsync(email, mail, time);
        var token = mail.TokenFor(email);
        var passwords = Enumerable.Range(0, 8).Select(i => $"Concurrent-Passw0rd-{i}").ToArray();

        var results = await Task.WhenAll(passwords.Select(p => ResetAsync(token, p, time)));

        Assert.Equal(1, results.Count(r => r.Status == ResetPasswordStatus.Completed));
        Assert.Equal(7, results.Count(r => r.Status == ResetPasswordStatus.InvalidOrExpiredToken));
        var winner = passwords[Array.FindIndex(results, r => r.Status == ResetPasswordStatus.Completed)];
        Assert.Equal(AuthenticationStatus.NoMembership, (await AuthTestSetup.LoginAsync(_fixture, email, winner, time)).Status);
        foreach (var loser in passwords.Where(p => p != winner))
            Assert.Equal(AuthenticationStatus.InvalidCredentials, (await AuthTestSetup.LoginAsync(_fixture, email, loser, time)).Status);
    }

    [Fact]
    public async Task Resetting_revokes_the_other_outstanding_reset_tokens_of_the_account()
    {
        var email = AuthTestSetup.NewEmail();
        var (accountId, _) = await AuthTestSetup.SeedAccountAsync(_fixture, email);
        var time = new TestTimeProvider();
        var mail = new FakeEmailSender();
        await ForgotAsync(email, mail, time);
        var sideToken = await AuthTestSetup.InsertTokenAsync(_fixture, h => AccountToken.CreatePasswordReset(accountId, h, time.GetUtcNow(), TimeSpan.FromMinutes(30)));

        await ResetAsync(mail.TokenFor(email), NewPassword, time);

        Assert.Equal(Invalid, await ResetAsync(sideToken, "Side-Channel-Passw0rd-1", time));
    }

    [Fact]
    public async Task Every_invalid_token_gives_the_same_result()
    {
        var email = AuthTestSetup.NewEmail();
        var (accountId, _) = await AuthTestSetup.SeedAccountAsync(_fixture, email);
        var time = new TestTimeProvider();
        var mail = new FakeEmailSender();
        await ForgotAsync(email, mail, time);
        var valid = mail.TokenFor(email);
        Assert.True(AccountTokenService.TryParse(valid, out var id, out var secret));
        var invite = await AuthTestSetup.InsertTokenAsync(_fixture, h => AccountToken.CreateInvite(1, AuthTestSetup.NewEmail(), null, null, h, time.GetUtcNow(), TimeSpan.FromDays(1), null));

        foreach (var raw in new[] { invite, $"{id:D}.{secret}x", $"{Guid.NewGuid():D}.{secret}", "garbage", "" })
            Assert.Equal(Invalid, await ResetAsync(raw, NewPassword, time));

        time.Advance(TimeSpan.FromMinutes(31));
        Assert.Equal(Invalid, await ResetAsync(valid, NewPassword, time)); // expired
        Assert.Equal(AuthenticationStatus.NoMembership, (await AuthTestSetup.LoginAsync(_fixture, email, AuthTestSetup.Password, time)).Status); // nothing changed
    }

    // ---- first-password setup --------------------------------------------------------------

    private async Task<(long AccountId, string Email)> AccountWithoutCredentialAsync()
    {
        var email = AuthTestSetup.NewEmail();
        await using var admin = _fixture.CreateAdminContext();
        var account = Account.Create(email, "Setup Person");
        admin.Accounts.Add(account);
        await admin.SaveChangesAsync();
        admin.ExternalIdentities.Add(ExternalIdentity.Link(account.Id, new(AuthTestSetup.Issuer, Guid.NewGuid().ToString("N"))));
        await admin.SaveChangesAsync();
        return (account.Id, email);
    }

    [Fact]
    public async Task A_setup_token_creates_the_first_password_exactly_once()
    {
        var (accountId, email) = await AccountWithoutCredentialAsync();
        var time = new TestTimeProvider();
        var token = await AuthTestSetup.InsertTokenAsync(_fixture, h => AccountToken.CreatePasswordSetup(accountId, h, time.GetUtcNow(), TimeSpan.FromHours(24)));
        Assert.Equal(AuthenticationStatus.InvalidCredentials, (await AuthTestSetup.LoginAsync(_fixture, email, NewPassword, time)).Status); // cannot sign in before setup

        Assert.Equal(ResetPasswordStatus.Completed, (await ResetAsync(token, NewPassword, time)).Status);

        Assert.Equal(AuthenticationStatus.NoMembership, (await AuthTestSetup.LoginAsync(_fixture, email, NewPassword, time)).Status);
        Assert.Equal(Invalid, await ResetAsync(token, "Second-Passw0rd-2026", time));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM identity.account_credentials WHERE account_id = @a", ("a", accountId)));
        await using var admin = _fixture.CreateAdminContext();
        Assert.Contains(await admin.AuthEvents.Where(e => e.AccountId == accountId).Select(e => e.EventType).ToListAsync(), t => t == "password_setup_completed");
    }

    [Fact]
    public async Task Token_purposes_must_fit_the_account_state()
    {
        var time = new TestTimeProvider();
        var (bareId, _) = await AccountWithoutCredentialAsync();
        var (withPasswordId, _) = await AuthTestSetup.SeedAccountAsync(_fixture, AuthTestSetup.NewEmail());
        var resetForBare = await AuthTestSetup.InsertTokenAsync(_fixture, h => AccountToken.CreatePasswordReset(bareId, h, time.GetUtcNow(), TimeSpan.FromMinutes(30)));
        var setupForExisting = await AuthTestSetup.InsertTokenAsync(_fixture, h => AccountToken.CreatePasswordSetup(withPasswordId, h, time.GetUtcNow(), TimeSpan.FromHours(24)));

        Assert.Equal(Invalid, await ResetAsync(resetForBare, NewPassword, time)); // nothing to reset
        Assert.Equal(Invalid, await ResetAsync(setupForExisting, NewPassword, time)); // cannot overwrite an existing password through setup
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM identity.account_credentials WHERE account_id = @a", ("a", bareId)));
    }

    // ---- hygiene ---------------------------------------------------------------------------

    [Fact]
    public async Task No_secret_or_password_reaches_the_audit_events_of_a_full_forgot_and_reset_cycle()
    {
        var email = AuthTestSetup.NewEmail();
        var (accountId, _) = await AuthTestSetup.SeedAccountAsync(_fixture, email);
        var mail = new FakeEmailSender();
        var time = new TestTimeProvider();
        await ForgotAsync(email, mail, time);
        var token = mail.TokenFor(email);
        await ResetAsync(token, "short", time);
        await ResetAsync(token, NewPassword, time);

        await using var admin = _fixture.CreateAdminContext();
        var hash = (await admin.AccountCredentials.SingleAsync(c => c.AccountId == accountId)).PasswordHash;
        var events = await admin.AuthEvents.Where(e => e.AccountId == accountId).ToListAsync();
        Assert.Contains(events, e => e.EventType == "password_reset_completed");
        var serialized = string.Join("\n", events.Select(e => $"{e.EventType}|{e.Outcome}|{e.Detail}|{e.CorrelationId}|{e.IpHash}"));
        foreach (var value in new[] { NewPassword, AuthTestSetup.Password, "short", token, token[(token.IndexOf('.') + 1)..], hash, email })
            Assert.DoesNotContain(value, serialized, StringComparison.OrdinalIgnoreCase);
    }
}
