using Access.Application;
using Access.Application.Authentication;
using Access.Domain.Authentication;
using Access.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Access.Tests.Integration.Authentication;

/// <summary>The production route to a tenant's first administrator: no default credential, one-time
/// setup token, refuses to run twice.</summary>
public sealed class TenantAdministratorBootstrapTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string AdminPassword = "First-Admin-Passw0rd-1";

    private readonly PostgresFixture _fixture;

    public TenantAdministratorBootstrapTests(PostgresFixture fixture) => _fixture = fixture;

    /// <summary>The registry is seeded once, sequentially, exactly like Host does at startup (the seeder itself is not
    /// meant to run concurrently — parallel seeding collides on the `actions` primary key).</summary>
    public async Task InitializeAsync()
    {
        await using var seed = _fixture.CreateAdminContext();
        await AccessActionCatalogSeeder.EnsureSeededAsync(seed, AccessActionCatalog.All);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<BootstrapTenantAdministratorResult> BootstrapAsync(Contracts.TenantId tenant, string email, TimeProvider time)
    {
        await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);
        return await AuthTestSetup.BootstrapAdmin(context, time)
            .HandleAsync(new BootstrapTenantAdministratorCommand(tenant, email, "First Admin"));
    }

    private Task<long> CountAsync(string sql, params (string, object)[] p) => AuthTestSetup.ScalarLongAsync(_fixture, sql, p);

    [Fact]
    public async Task Bootstrap_creates_an_active_administrator_without_a_password_and_hands_out_a_setup_token_once()
    {
        var tenant = AuthTestSetup.NewTenant();
        var email = AuthTestSetup.NewEmail();
        var time = new TestTimeProvider();

        var result = await BootstrapAsync(tenant, email, time);

        Assert.Equal(BootstrapTenantAdministratorStatus.Completed, result.Status);
        var accountId = result.AccountId!.Value;
        await using var admin = _fixture.CreateAdminContext();
        Assert.Equal(MembershipStatus.Active, (await admin.TenantMemberships.SingleAsync(m => m.AccountId == accountId && m.TenantId == tenant)).Status);
        Assert.Equal(0, await admin.AccountCredentials.CountAsync(c => c.AccountId == accountId)); // no password exists anywhere
        Assert.Equal(1, await admin.RoleAssignments.CountAsync(a => a.TenantId == tenant && a.AccountId == accountId));
        Assert.Equal(result.Principal, (await admin.ExternalIdentities.SingleAsync(e => e.AccountId == accountId)).Principal);

        var setupToken = Assert.IsType<string>(result.SetupToken);
        var stored = await AuthTestSetup.LoadTokenAsync(_fixture, setupToken);
        Assert.Equal(AccountTokenPurpose.PasswordSetup, stored.Purpose);
        Assert.Equal(accountId, stored.AccountId);
        Assert.Equal(time.GetUtcNow().AddHours(24), stored.ExpiresAt);
        Assert.DoesNotContain(setupToken[(setupToken.IndexOf('.') + 1)..], stored.TokenHash); // only the hash is stored
    }

    [Fact]
    public async Task The_administrator_cannot_sign_in_until_the_setup_token_is_redeemed_and_then_holds_the_grants()
    {
        var tenant = AuthTestSetup.NewTenant();
        var email = AuthTestSetup.NewEmail();
        var time = new TestTimeProvider();
        var result = await BootstrapAsync(tenant, email, time);
        Assert.Equal(AuthenticationStatus.InvalidCredentials, (await AuthTestSetup.LoginAsync(_fixture, email, AdminPassword, time)).Status);

        await using (var context = await AuthTestSetup.RuntimeContextAsync(_fixture))
        {
            var setup = await AuthTestSetup.Reset(context, time).HandleAsync(new ResetPasswordCommand(result.SetupToken!, AdminPassword));
            Assert.Equal(ResetPasswordStatus.Completed, setup.Status);
        }

        var login = await AuthTestSetup.LoginAsync(_fixture, email, AdminPassword, time);
        Assert.Equal(AuthenticationStatus.Authenticated, login.Status);
        Assert.Equal(tenant.Value, login.SelectedTenantId);

        // The bootstrap role includes `identity.membership.invite`: the fresh administrator can onboard people.
        var mail = new FakeEmailSender();
        await using var actorContext = await AuthTestSetup.RuntimeContextAsync(_fixture);
        var invite = await AuthTestSetup.Invitations(actorContext, mail, time)
            .HandleAsync(new CreateInvitationCommand(AuthTestSetup.Actor(tenant, result.Principal!.Value), AuthTestSetup.NewEmail()));
        Assert.Equal(CreateInvitationStatus.Accepted, invite.Status);
    }

    [Fact]
    public async Task The_setup_token_is_single_use_and_expires()
    {
        var tenant = AuthTestSetup.NewTenant();
        var time = new TestTimeProvider();
        var result = await BootstrapAsync(tenant, AuthTestSetup.NewEmail(), time);
        var otherTenant = AuthTestSetup.NewTenant();
        var expiring = await BootstrapAsync(otherTenant, AuthTestSetup.NewEmail(), time);

        await using (var context = await AuthTestSetup.RuntimeContextAsync(_fixture))
        {
            Assert.Equal(ResetPasswordStatus.Completed, (await AuthTestSetup.Reset(context, time).HandleAsync(new ResetPasswordCommand(result.SetupToken!, AdminPassword))).Status);
            Assert.Equal(ResetPasswordStatus.InvalidOrExpiredToken, (await AuthTestSetup.Reset(context, time).HandleAsync(new ResetPasswordCommand(result.SetupToken!, "Replayed-Passw0rd-2"))).Status);
        }

        time.Advance(TimeSpan.FromHours(25));
        await using var late = await AuthTestSetup.RuntimeContextAsync(_fixture);
        Assert.Equal(ResetPasswordStatus.InvalidOrExpiredToken, (await AuthTestSetup.Reset(late, time).HandleAsync(new ResetPasswordCommand(expiring.SetupToken!, AdminPassword))).Status);
    }

    [Fact]
    public async Task A_second_bootstrap_of_the_same_tenant_is_refused_and_creates_nothing()
    {
        var tenant = AuthTestSetup.NewTenant();
        var time = new TestTimeProvider();
        await BootstrapAsync(tenant, AuthTestSetup.NewEmail(), time);
        var accountsBefore = await CountAsync("SELECT count(*) FROM identity.accounts");
        var tokensBefore = await CountAsync("SELECT count(*) FROM identity.account_tokens");

        var second = await BootstrapAsync(tenant, AuthTestSetup.NewEmail(), time);

        Assert.Equal(new BootstrapTenantAdministratorResult(BootstrapTenantAdministratorStatus.AlreadyBootstrapped), second);
        Assert.Equal(accountsBefore, await CountAsync("SELECT count(*) FROM identity.accounts"));
        Assert.Equal(tokensBefore, await CountAsync("SELECT count(*) FROM identity.account_tokens"));
    }

    [Fact]
    public async Task Concurrent_bootstraps_of_one_tenant_leave_exactly_one_administrator()
    {
        var tenant = AuthTestSetup.NewTenant();
        var time = new TestTimeProvider();

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => BootstrapAsync(tenant, AuthTestSetup.NewEmail(), time)));

        Assert.Equal(1, results.Count(r => r.Status == BootstrapTenantAdministratorStatus.Completed));
        Assert.All(results.Where(r => r.Status != BootstrapTenantAdministratorStatus.Completed),
            r => Assert.Equal(BootstrapTenantAdministratorStatus.AlreadyBootstrapped, r.Status));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM access.role_assignments WHERE tenant_id = @t", ("t", tenant.Value)));
    }

    [Theory]
    [InlineData("not-an-email", "Admin")]
    [InlineData("", "Admin")]
    [InlineData("valid@example.com", " ")]
    public async Task Invalid_input_is_rejected_before_anything_is_created(string email, string displayName)
    {
        var tenant = AuthTestSetup.NewTenant();
        await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);

        var result = await AuthTestSetup.BootstrapAdmin(context, new TestTimeProvider())
            .HandleAsync(new BootstrapTenantAdministratorCommand(tenant, email, displayName));

        Assert.Equal(BootstrapTenantAdministratorStatus.InvalidEmail, result.Status);
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM identity.tenant_memberships WHERE tenant_id = @t", ("t", tenant.Value)));
    }

    [Fact]
    public async Task The_event_and_evidence_never_contain_the_setup_token()
    {
        var tenant = AuthTestSetup.NewTenant();
        var result = await BootstrapAsync(tenant, AuthTestSetup.NewEmail(), new TestTimeProvider());
        var token = result.SetupToken!;

        await using var admin = _fixture.CreateAdminContext();
        var events = await admin.AuthEvents.Where(e => e.TenantId == tenant.Value).ToListAsync();
        Assert.Contains(events, e => e.EventType == "bootstrap_completed" && e.Outcome == "success");
        var text = string.Join("\n", events.Select(e => $"{e.Outcome}|{e.Detail}|{e.CorrelationId}"))
                   + string.Join("\n", await admin.EvidenceRecords.Where(e => e.TenantId == tenant).Select(e => e.Detail).ToListAsync())
                   + string.Join("\n", await admin.OutboxMessages.Where(m => m.TenantId == tenant).Select(m => m.Payload).ToListAsync());
        Assert.DoesNotContain(token, text);
        Assert.DoesNotContain(token[(token.IndexOf('.') + 1)..], text);
    }
}
