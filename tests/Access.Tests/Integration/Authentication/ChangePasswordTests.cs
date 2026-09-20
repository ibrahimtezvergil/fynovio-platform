using Access.Application.Authentication;
using Access.Domain.Authentication;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Access.Tests.Integration.Authentication;

public sealed class ChangePasswordTests : IClassFixture<PostgresFixture>
{
    private const string NewPassword = "Changed-Passw0rd-2026";

    private readonly PostgresFixture _fixture;

    public ChangePasswordTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<ChangePasswordResult> ChangeAsync(
        long accountId, Guid sessionId, string current, string next, TimeProvider time, LockoutOptions? lockout = null)
    {
        await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);
        return await AuthTestSetup.ChangePassword(context, time, lockout)
            .HandleAsync(new ChangePasswordCommand(accountId, sessionId, current, next));
    }

    private Task<long> CountAsync(string sql, params (string, object)[] p) => AuthTestSetup.ScalarLongAsync(_fixture, sql, p);

    [Fact]
    public async Task Changing_replaces_the_password_and_keeps_only_the_callers_session()
    {
        var email = AuthTestSetup.NewEmail();
        var (accountId, _) = await AuthTestSetup.SeedAccountAsync(_fixture, email);
        var bystanderEmail = AuthTestSetup.NewEmail();
        await AuthTestSetup.SeedAccountAsync(_fixture, bystanderEmail);
        var time = new TestTimeProvider();
        var current = await AuthTestSetup.LoginAsync(_fixture, email, AuthTestSetup.Password, time);
        var other = await AuthTestSetup.LoginAsync(_fixture, email, AuthTestSetup.Password, time);
        var third = await AuthTestSetup.LoginAsync(_fixture, email, AuthTestSetup.Password, time);
        var bystander = await AuthTestSetup.LoginAsync(_fixture, bystanderEmail, AuthTestSetup.Password, time);

        var result = await ChangeAsync(accountId, current.SessionId!.Value, AuthTestSetup.Password, NewPassword, time);

        Assert.Equal(new ChangePasswordResult(ChangePasswordStatus.Completed), result);
        Assert.NotEqual(AuthenticationStatus.InvalidCredentials, (await AuthTestSetup.LoginAsync(_fixture, email, NewPassword, time)).Status);
        Assert.Equal(AuthenticationStatus.InvalidCredentials, (await AuthTestSetup.LoginAsync(_fixture, email, AuthTestSetup.Password, time)).Status);

        Assert.Equal(RefreshResult.Success, (await AuthTestSetup.RefreshAsync(_fixture, current.RefreshCookie!, time)).Status); // the caller's own session lives on
        Assert.Equal(RefreshResult.SessionInvalid, (await AuthTestSetup.RefreshAsync(_fixture, other.RefreshCookie!, time)).Status);
        Assert.Equal(RefreshResult.SessionInvalid, (await AuthTestSetup.RefreshAsync(_fixture, third.RefreshCookie!, time)).Status);
        Assert.Equal(RefreshResult.Success, (await AuthTestSetup.RefreshAsync(_fixture, bystander.RefreshCookie!, time)).Status); // other accounts untouched
        Assert.Equal(2, await CountAsync("SELECT count(*) FROM identity.auth_sessions WHERE account_id = @a AND revoked_reason = 'password_changed'", ("a", accountId)));
    }

    [Fact]
    public async Task A_wrong_current_password_fails_counts_toward_lockout_and_changes_nothing()
    {
        var email = AuthTestSetup.NewEmail();
        var (accountId, _) = await AuthTestSetup.SeedAccountAsync(_fixture, email);
        var time = new TestTimeProvider();
        var session = await AuthTestSetup.LoginAsync(_fixture, email, AuthTestSetup.Password, time);
        var lockout = new LockoutOptions { MaxFailedAttempts = 3, LockoutMinutes = 15 };

        for (var i = 1; i <= 3; i++)
        {
            var failed = await ChangeAsync(accountId, session.SessionId!.Value, "Not-The-Password-1", NewPassword, time, lockout);
            Assert.Equal(new ChangePasswordResult(ChangePasswordStatus.InvalidCredentials), failed);
            Assert.Equal(i, await CountAsync("SELECT failed_attempts FROM identity.account_credentials WHERE account_id = @a", ("a", accountId)));
        }

        // Locked: even the right current password is refused until the window passes.
        Assert.Equal(ChangePasswordStatus.InvalidCredentials, (await ChangeAsync(accountId, session.SessionId!.Value, AuthTestSetup.Password, NewPassword, time, lockout)).Status);
        time.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal(AuthenticationStatus.NoMembership, (await AuthTestSetup.LoginAsync(_fixture, email, AuthTestSetup.Password, time)).Status); // password never changed
    }

    [Fact]
    public async Task The_new_password_must_differ_and_meet_the_policy_and_only_a_caller_who_proved_the_current_one_learns_which()
    {
        var email = AuthTestSetup.NewEmail();
        var (accountId, _) = await AuthTestSetup.SeedAccountAsync(_fixture, email);
        var time = new TestTimeProvider();
        var session = await AuthTestSetup.LoginAsync(_fixture, email, AuthTestSetup.Password, time);
        var other = await AuthTestSetup.LoginAsync(_fixture, email, AuthTestSetup.Password, time);
        var sessionId = session.SessionId!.Value;

        var same = await ChangeAsync(accountId, sessionId, AuthTestSetup.Password, AuthTestSetup.Password, time);
        var weak = await ChangeAsync(accountId, sessionId, AuthTestSetup.Password, "short", time);
        var asEmail = await ChangeAsync(accountId, sessionId, AuthTestSetup.Password, email, time);
        var wrongCurrentAndWeak = await ChangeAsync(accountId, sessionId, "Not-The-Password-1", "short", time);

        Assert.Equal(ChangePasswordStatus.PolicyViolation, same.Status);
        Assert.Contains("same_as_current", same.PolicyViolations!);
        Assert.Contains("too_short", weak.PolicyViolations!);
        Assert.Contains("equals_email", asEmail.PolicyViolations!);
        Assert.Equal(new ChangePasswordResult(ChangePasswordStatus.InvalidCredentials), wrongCurrentAndWeak); // no policy hints for an unproven caller
        Assert.Equal(RefreshResult.Success, (await AuthTestSetup.RefreshAsync(_fixture, other.RefreshCookie!, time)).Status); // a rejected change revokes nothing
    }

    [Fact]
    public async Task An_unknown_account_gets_the_same_answer_as_a_wrong_password()
    {
        var time = new TestTimeProvider();

        var result = await ChangeAsync(long.MaxValue - 7, Guid.NewGuid(), "whatever-Passw0rd-1", NewPassword, time);

        Assert.Equal(new ChangePasswordResult(ChangePasswordStatus.InvalidCredentials), result);
    }

    [Fact]
    public async Task Concurrent_changes_never_escape_as_errors_and_exactly_one_wins()
    {
        var email = AuthTestSetup.NewEmail();
        var (accountId, _) = await AuthTestSetup.SeedAccountAsync(_fixture, email);
        var time = new TestTimeProvider();
        var session = await AuthTestSetup.LoginAsync(_fixture, email, AuthTestSetup.Password, time);
        var passwords = Enumerable.Range(0, 6).Select(i => $"Racing-Passw0rd-{i}-2026").ToArray();

        var results = await Task.WhenAll(passwords.Select(p => ChangeAsync(accountId, session.SessionId!.Value, AuthTestSetup.Password, p, time)));

        Assert.Equal(1, results.Count(r => r.Status == ChangePasswordStatus.Completed));
        Assert.All(results.Where(r => r.Status != ChangePasswordStatus.Completed),
            r => Assert.Contains(r.Status, new[] { ChangePasswordStatus.Conflict, ChangePasswordStatus.InvalidCredentials }));
        var winner = passwords[Array.FindIndex(results, r => r.Status == ChangePasswordStatus.Completed)];
        Assert.NotEqual(AuthenticationStatus.InvalidCredentials, (await AuthTestSetup.LoginAsync(_fixture, email, winner, time)).Status);
    }

    [Fact]
    public async Task Changing_the_password_revokes_outstanding_reset_tokens()
    {
        var email = AuthTestSetup.NewEmail();
        var (accountId, _) = await AuthTestSetup.SeedAccountAsync(_fixture, email);
        var time = new TestTimeProvider();
        var session = await AuthTestSetup.LoginAsync(_fixture, email, AuthTestSetup.Password, time);
        var reset = await AuthTestSetup.InsertTokenAsync(_fixture, h => AccountToken.CreatePasswordReset(accountId, h, time.GetUtcNow(), TimeSpan.FromMinutes(30)));

        await ChangeAsync(accountId, session.SessionId!.Value, AuthTestSetup.Password, NewPassword, time);

        Assert.NotNull((await AuthTestSetup.LoadTokenAsync(_fixture, reset)).RevokedAt);
    }

    [Fact]
    public async Task No_password_or_hash_reaches_the_audit_events()
    {
        var email = AuthTestSetup.NewEmail();
        var (accountId, _) = await AuthTestSetup.SeedAccountAsync(_fixture, email);
        var time = new TestTimeProvider();
        var session = await AuthTestSetup.LoginAsync(_fixture, email, AuthTestSetup.Password, time);
        await ChangeAsync(accountId, session.SessionId!.Value, "Not-The-Password-1", NewPassword, time);
        await ChangeAsync(accountId, session.SessionId!.Value, AuthTestSetup.Password, NewPassword, time);

        await using var admin = _fixture.CreateAdminContext();
        var hash = (await admin.AccountCredentials.SingleAsync(c => c.AccountId == accountId)).PasswordHash;
        var events = await admin.AuthEvents.Where(e => e.AccountId == accountId && e.EventType == "password_change").ToListAsync();
        Assert.Contains(events, e => e.Outcome == "success");
        Assert.Contains(events, e => e.Outcome == "bad_password");
        var serialized = string.Join("\n", events.Select(e => $"{e.Outcome}|{e.Detail}|{e.CorrelationId}|{e.IpHash}"));
        foreach (var value in new[] { NewPassword, AuthTestSetup.Password, "Not-The-Password-1", hash })
            Assert.DoesNotContain(value, serialized);
    }
}
