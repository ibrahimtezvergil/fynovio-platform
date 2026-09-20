using Access.Application.Authentication;
using Access.Domain.Authentication;
using Access.Domain.Identity;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Access.Tests.Integration.Authentication;

public sealed class InvitationAcceptanceTests : IClassFixture<PostgresFixture>
{
    private const string NewPassword = "Brand-New-Passw0rd!";

    private readonly PostgresFixture _fixture;

    public InvitationAcceptanceTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<(TenantId Tenant, PrincipalRef Admin)> TenantWithAdminAsync()
    {
        var tenant = AuthTestSetup.NewTenant();
        var (_, admin) = await AuthTestSetup.SeedTenantAdminAsync(_fixture, tenant);
        return (tenant, admin);
    }

    private async Task<AcceptInvitationResult> AcceptAsync(string token, string password, TimeProvider time, string? displayName = null, LockoutOptions? lockout = null)
    {
        await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);
        return await AuthTestSetup.Acceptor(context, time, lockout).HandleAsync(new AcceptInvitationCommand(token, password, displayName));
    }

    private Task<long> CountAsync(string sql, params (string, object)[] p) => AuthTestSetup.ScalarLongAsync(_fixture, sql, p);

    [Fact]
    public async Task Accepting_for_a_new_address_creates_the_account_membership_and_session()
    {
        var (tenant, admin) = await TenantWithAdminAsync();
        var time = new TestTimeProvider();
        var email = AuthTestSetup.NewEmail();
        var token = await AuthTestSetup.InviteAsync(_fixture, tenant, admin, email, time, "Nova Kaya");

        var result = await AcceptAsync(token, NewPassword, time);

        Assert.Equal(AcceptInvitationStatus.Accepted, result.Status);
        var session = Assert.IsType<AuthenticateResult>(result.Session);
        Assert.Equal(AuthenticationStatus.Authenticated, session.Status);
        Assert.Equal(tenant.Value, session.SelectedTenantId);
        Assert.Equal("Nova Kaya", session.Account!.DisplayName);
        Assert.True(AccountTokenService.TryParse(session.RefreshCookie, out _, out _));

        var normalized = EmailNormalizer.Normalize(email);
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM identity.account_credentials WHERE login_email_normalized = @e", ("e", normalized)));
        Assert.Equal(1, await CountAsync(
            "SELECT count(*) FROM identity.external_identities e JOIN identity.account_credentials c ON c.account_id = e.account_id WHERE c.login_email_normalized = @e AND e.issuer = @i",
            ("e", normalized), ("i", AuthTestSetup.Issuer)));

        await using var admins = _fixture.CreateAdminContext();
        var accountId = session.AccountId!.Value;
        var membership = await admins.TenantMemberships.SingleAsync(m => m.AccountId == accountId && m.TenantId == tenant);
        Assert.Equal(MembershipStatus.Active, membership.Status);
        var identity = await admins.ExternalIdentities.SingleAsync(e => e.AccountId == accountId);
        Assert.DoesNotContain(normalized, identity.Subject);
        Assert.Matches("^[0-9a-f]{32}$", identity.Subject); // 128 random bits, hex — nothing derived from the address or the id
        Assert.Equal(session.Principal, identity.Principal);
        var credential = await admins.AccountCredentials.SingleAsync(c => c.AccountId == accountId);
        Assert.NotEqual(NewPassword, credential.PasswordHash);
        Assert.NotNull((await AuthTestSetup.LoadTokenAsync(_fixture, token)).ConsumedAt);
        Assert.False((await admins.AuthSessions.SingleAsync(s => s.Id == session.SessionId)).IsRevoked);

        // Evidence + outbox for the activation, in the tenant, without the password or the token.
        var evidence = await admins.EvidenceRecords.SingleAsync(e => e.TenantId == tenant && e.Action == "TenantMembership.Activate");
        Assert.DoesNotContain(NewPassword, evidence.Detail);
        Assert.DoesNotContain(token, evidence.Detail);
        Assert.Equal(1, await admins.OutboxMessages.CountAsync(m => m.TenantId == tenant && m.EventType == "enterprise.access.membership.activated.v1"));
    }

    [Fact]
    public async Task The_new_account_can_then_sign_in_with_the_chosen_password()
    {
        var (tenant, admin) = await TenantWithAdminAsync();
        var time = new TestTimeProvider();
        var email = AuthTestSetup.NewEmail();
        await AcceptAsync(await AuthTestSetup.InviteAsync(_fixture, tenant, admin, email, time), NewPassword, time);

        await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);
        var login = await new AuthenticateHandler(context, new PasswordService(), new LockoutOptions(), AuthTestSetup.Session, new AuthEventWriter(context), time)
            .HandleAsync(new AuthenticateCommand(email.ToUpperInvariant(), NewPassword));

        Assert.Equal(AuthenticationStatus.Authenticated, login.Status);
    }

    [Fact]
    public async Task A_token_can_be_accepted_only_once()
    {
        var (tenant, admin) = await TenantWithAdminAsync();
        var time = new TestTimeProvider();
        var email = AuthTestSetup.NewEmail();
        var token = await AuthTestSetup.InviteAsync(_fixture, tenant, admin, email, time);

        Assert.Equal(AcceptInvitationStatus.Accepted, (await AcceptAsync(token, NewPassword, time)).Status);
        var second = await AcceptAsync(token, NewPassword, time);

        Assert.Equal(new AcceptInvitationResult(AcceptInvitationStatus.InvalidOrExpiredToken), second);
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM identity.account_credentials WHERE login_email_normalized = @e", ("e", EmailNormalizer.Normalize(email))));
    }

    [Fact]
    public async Task Concurrent_accepts_of_one_token_produce_exactly_one_winner_and_one_account()
    {
        var (tenant, admin) = await TenantWithAdminAsync();
        var time = new TestTimeProvider();

        for (var round = 0; round < 3; round++)
        {
            var email = AuthTestSetup.NewEmail();
            var token = await AuthTestSetup.InviteAsync(_fixture, tenant, admin, email, time);

            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => AcceptAsync(token, NewPassword, time)));

            Assert.Equal(1, results.Count(r => r.Status == AcceptInvitationStatus.Accepted));
            Assert.Equal(7, results.Count(r => r.Status == AcceptInvitationStatus.InvalidOrExpiredToken));
            var normalized = EmailNormalizer.Normalize(email);
            Assert.Equal(1, await CountAsync("SELECT count(*) FROM identity.account_credentials WHERE login_email_normalized = @e", ("e", normalized)));
            Assert.Equal(1, await CountAsync(
                "SELECT count(*) FROM identity.tenant_memberships m JOIN identity.account_credentials c ON c.account_id = m.account_id WHERE c.login_email_normalized = @e", ("e", normalized)));
            Assert.Equal(1, await CountAsync(
                "SELECT count(*) FROM identity.auth_sessions s JOIN identity.account_credentials c ON c.account_id = s.account_id WHERE c.login_email_normalized = @e", ("e", normalized)));
        }
    }

    /// <summary>For a NEW address the credential's unique index would also stop a double redemption, so this is the
    /// test that proves the token's own single-use guard: an existing account has no such second line of defence.</summary>
    [Fact]
    public async Task Concurrent_accepts_for_an_existing_account_still_produce_exactly_one_session()
    {
        var (tenant, admin) = await TenantWithAdminAsync();
        var time = new TestTimeProvider();

        for (var round = 0; round < 3; round++)
        {
            var email = AuthTestSetup.NewEmail();
            var (accountId, _) = await AuthTestSetup.SeedAccountAsync(_fixture, email);
            var token = await AuthTestSetup.InviteAsync(_fixture, tenant, admin, email, time);

            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => AcceptAsync(token, AuthTestSetup.Password, time)));

            Assert.Equal(1, results.Count(r => r.Status == AcceptInvitationStatus.Accepted));
            Assert.Equal(7, results.Count(r => r.Status == AcceptInvitationStatus.InvalidOrExpiredToken));
            Assert.Equal(1, await CountAsync("SELECT count(*) FROM identity.auth_sessions WHERE account_id = @a", ("a", accountId)));
            Assert.Equal(1, await CountAsync("SELECT count(*) FROM identity.tenant_memberships WHERE account_id = @a AND tenant_id = @t", ("a", accountId), ("t", tenant.Value)));
        }
    }

    [Fact]
    public async Task A_password_that_breaks_the_policy_creates_nothing_and_keeps_the_token_usable()
    {
        var (tenant, admin) = await TenantWithAdminAsync();
        var time = new TestTimeProvider();
        var email = AuthTestSetup.NewEmail();
        var token = await AuthTestSetup.InviteAsync(_fixture, tenant, admin, email, time);

        var weak = await AcceptAsync(token, "short", time);
        var sameAsEmail = await AcceptAsync(token, email, time);

        Assert.Equal(AcceptInvitationStatus.PolicyViolation, weak.Status);
        Assert.NotEmpty(weak.PolicyViolations!);
        Assert.Equal(AcceptInvitationStatus.PolicyViolation, sameAsEmail.Status);
        Assert.Null(weak.Session);
        Assert.Null((await AuthTestSetup.LoadTokenAsync(_fixture, token)).ConsumedAt);
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM identity.account_credentials WHERE login_email_normalized = @e", ("e", EmailNormalizer.Normalize(email))));

        Assert.Equal(AcceptInvitationStatus.Accepted, (await AcceptAsync(token, NewPassword, time)).Status); // and the retry works
    }

    [Fact]
    public async Task An_existing_account_must_prove_its_password_and_gains_the_membership()
    {
        var (tenant, admin) = await TenantWithAdminAsync();
        var otherTenant = AuthTestSetup.NewTenant();
        var time = new TestTimeProvider();
        var email = AuthTestSetup.NewEmail();
        var (accountId, _) = await AuthTestSetup.SeedAccountAsync(_fixture, email, tenant: otherTenant); // already a member elsewhere
        var token = await AuthTestSetup.InviteAsync(_fixture, tenant, admin, email, time);

        var result = await AcceptAsync(token, AuthTestSetup.Password, time);

        Assert.Equal(AcceptInvitationStatus.Accepted, result.Status);
        Assert.Equal(AuthenticationStatus.TenantSelectionRequired, result.Session!.Status); // two memberships now
        Assert.Equivalent(new[] { tenant.Value, otherTenant.Value }, result.Session.MembershipTenantIds);
        Assert.Equal(accountId, result.Session.AccountId);
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM identity.account_credentials WHERE login_email_normalized = @e", ("e", EmailNormalizer.Normalize(email))));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM identity.accounts WHERE id = @i", ("i", accountId)));
        Assert.NotNull((await AuthTestSetup.LoadTokenAsync(_fixture, token)).ConsumedAt);
    }

    [Fact]
    public async Task A_wrong_password_for_an_existing_account_fails_counts_toward_lockout_and_keeps_the_token()
    {
        var (tenant, admin) = await TenantWithAdminAsync();
        var time = new TestTimeProvider();
        var email = AuthTestSetup.NewEmail();
        var (accountId, _) = await AuthTestSetup.SeedAccountAsync(_fixture, email);
        var token = await AuthTestSetup.InviteAsync(_fixture, tenant, admin, email, time);
        var lockout = new LockoutOptions { MaxFailedAttempts = 3, LockoutMinutes = 15 };

        for (var i = 1; i <= 3; i++)
        {
            var failed = await AcceptAsync(token, "Wrong-Password-1234", time, lockout: lockout);
            Assert.Equal(new AcceptInvitationResult(AcceptInvitationStatus.InvalidCredentials), failed);
            Assert.Equal(i, await CountAsync("SELECT failed_attempts FROM identity.account_credentials WHERE account_id = @a", ("a", accountId)));
        }

        Assert.Null((await AuthTestSetup.LoadTokenAsync(_fixture, token)).ConsumedAt);
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM identity.tenant_memberships WHERE account_id = @a AND tenant_id = @t", ("a", accountId), ("t", tenant.Value)));

        // Locked now: even the right password is refused until the window passes...
        Assert.Equal(AcceptInvitationStatus.InvalidCredentials, (await AcceptAsync(token, AuthTestSetup.Password, time, lockout: lockout)).Status);
        time.Advance(TimeSpan.FromMinutes(16));
        // ...and afterwards it works and resets the counter.
        Assert.Equal(AcceptInvitationStatus.Accepted, (await AcceptAsync(token, AuthTestSetup.Password, time, lockout: lockout)).Status);
        Assert.Equal(0, await CountAsync("SELECT failed_attempts FROM identity.account_credentials WHERE account_id = @a", ("a", accountId)));
    }

    [Fact]
    public async Task A_disabled_membership_is_never_re_enabled_by_an_invitation()
    {
        var (tenant, admin) = await TenantWithAdminAsync();
        var time = new TestTimeProvider();
        var email = AuthTestSetup.NewEmail();
        var (accountId, _) = await AuthTestSetup.SeedAccountAsync(_fixture, email, tenant: tenant);
        await using (var admins = _fixture.CreateAdminContext())
        {
            var membership = await admins.TenantMemberships.SingleAsync(m => m.AccountId == accountId && m.TenantId == tenant);
            membership.Disable();
            await admins.SaveChangesAsync();
        }
        var token = await AuthTestSetup.InviteAsync(_fixture, tenant, admin, email, time);

        var result = await AcceptAsync(token, AuthTestSetup.Password, time);

        Assert.Equal(new AcceptInvitationResult(AcceptInvitationStatus.InvalidOrExpiredToken), result);
        await using var verify = _fixture.CreateAdminContext();
        Assert.Equal(MembershipStatus.Disabled, (await verify.TenantMemberships.SingleAsync(m => m.AccountId == accountId && m.TenantId == tenant)).Status);
        Assert.Equal(0, await verify.AuthSessions.CountAsync(s => s.AccountId == accountId));
    }

    [Fact]
    public async Task A_pending_membership_is_activated()
    {
        var (tenant, admin) = await TenantWithAdminAsync();
        var time = new TestTimeProvider();
        var email = AuthTestSetup.NewEmail();
        var (accountId, _) = await AuthTestSetup.SeedAccountAsync(_fixture, email);
        await AuthTestSetupPending(accountId, tenant);
        var token = await AuthTestSetup.InviteAsync(_fixture, tenant, admin, email, time);

        var result = await AcceptAsync(token, AuthTestSetup.Password, time);

        Assert.Equal(AcceptInvitationStatus.Accepted, result.Status);
        await using var verify = _fixture.CreateAdminContext();
        Assert.Equal(MembershipStatus.Active, (await verify.TenantMemberships.SingleAsync(m => m.AccountId == accountId && m.TenantId == tenant)).Status);
    }

    private async Task AuthTestSetupPending(long accountId, TenantId tenant)
    {
        await using var admins = _fixture.CreateAdminContext();
        admins.TenantMemberships.Add(TenantMembership.Invite(tenant, accountId));
        await admins.SaveChangesAsync();
    }

    [Fact]
    public async Task Every_invalid_token_produces_the_same_result_and_creates_nothing()
    {
        var (tenant, admin) = await TenantWithAdminAsync();
        var time = new TestTimeProvider();
        var email = AuthTestSetup.NewEmail();
        var valid = await AuthTestSetup.InviteAsync(_fixture, tenant, admin, email, time);
        Assert.True(AccountTokenService.TryParse(valid, out var id, out var secret));
        var (adminAccountId, _) = await AuthTestSetup.SeedAccountAsync(_fixture, AuthTestSetup.NewEmail());
        var resetToken = await AuthTestSetup.InsertTokenAsync(_fixture, h => AccountToken.CreatePasswordReset(adminAccountId, h, time.GetUtcNow(), TimeSpan.FromMinutes(30)));
        var expected = new AcceptInvitationResult(AcceptInvitationStatus.InvalidOrExpiredToken);

        foreach (var raw in new[] { resetToken, $"{id:D}.{secret}x", $"{Guid.NewGuid():D}.{secret}", "garbage", "" })
            Assert.Equal(expected, await AcceptAsync(raw, NewPassword, time));

        time.Advance(TimeSpan.FromDays(8));
        Assert.Equal(expected, await AcceptAsync(valid, NewPassword, time)); // expired
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM identity.account_credentials WHERE login_email_normalized = @e", ("e", EmailNormalizer.Normalize(email))));
    }

    [Fact]
    public async Task No_password_token_or_hash_ever_reaches_the_audit_events()
    {
        var (tenant, admin) = await TenantWithAdminAsync();
        var time = new TestTimeProvider();
        var email = AuthTestSetup.NewEmail();
        var (accountId, _) = await AuthTestSetup.SeedAccountAsync(_fixture, email);
        var token = await AuthTestSetup.InviteAsync(_fixture, tenant, admin, email, time);
        await AcceptAsync(token, "Wrong-Password-1234", time);
        await AcceptAsync(token, AuthTestSetup.Password, time);

        await using var admins = _fixture.CreateAdminContext();
        var hash = (await admins.AccountCredentials.SingleAsync(c => c.AccountId == accountId)).PasswordHash;
        var events = await admins.AuthEvents.Where(e => e.AccountId == accountId || e.TenantId == tenant.Value).ToListAsync();
        Assert.NotEmpty(events);
        var serialized = string.Join("\n", events.Select(e => $"{e.EventType}|{e.Outcome}|{e.Detail}|{e.CorrelationId}|{e.IpHash}"));
        foreach (var secretValue in new[] { "Wrong-Password-1234", AuthTestSetup.Password, token, token[(token.IndexOf('.') + 1)..], hash })
            Assert.DoesNotContain(secretValue, serialized);
        Assert.Contains(events, e => e.EventType == "invite_accepted" && e.Outcome == "bad_password");
        Assert.Contains(events, e => e.EventType == "invite_accepted" && e.Outcome == "success");
    }
}
