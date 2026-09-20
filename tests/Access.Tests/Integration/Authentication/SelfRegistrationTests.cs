using Access.Application.Authentication;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Access.Tests.Integration.Authentication;

public sealed class SelfRegistrationTests : IClassFixture<PostgresFixture>
{
    private const string Password = "Registered-Passw0rd-1";

    private readonly PostgresFixture _fixture;

    public SelfRegistrationTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<RegisterAccountResult> RegisterAsync(string email, string displayName, string password, TimeProvider? time = null)
    {
        await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);
        return await AuthTestSetup.Register(context, time ?? new TestTimeProvider())
            .HandleAsync(new RegisterAccountCommand(email, displayName, password));
    }

    private Task<long> CountAsync(string sql, params (string, object)[] p) => AuthTestSetup.ScalarLongAsync(_fixture, sql, p);

    [Fact]
    public async Task Registering_creates_an_identity_and_nothing_that_grants_access()
    {
        var email = AuthTestSetup.NewEmail();

        var result = await RegisterAsync(email.ToUpperInvariant(), "Self Registered", Password);

        Assert.Equal(new RegisterAccountResult(RegisterAccountStatus.Accepted), result);
        var normalized = EmailNormalizer.Normalize(email);
        var accountId = await CountAsync("SELECT account_id FROM identity.account_credentials WHERE login_email_normalized = @e", ("e", normalized));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM identity.external_identities WHERE account_id = @a AND issuer = @i", ("a", accountId), ("i", AuthTestSetup.Issuer)));
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM identity.tenant_memberships WHERE account_id = @a", ("a", accountId)));
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM access.role_assignments WHERE account_id = @a", ("a", accountId)));
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM identity.auth_sessions WHERE account_id = @a", ("a", accountId))); // no session either: the user signs in
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM identity.account_tokens WHERE account_id = @a", ("a", accountId)));
    }

    [Fact]
    public async Task A_registered_account_can_sign_in_but_lands_in_the_no_membership_state()
    {
        var email = AuthTestSetup.NewEmail();
        await RegisterAsync(email, "Self Registered", Password);

        var login = await AuthTestSetup.LoginAsync(_fixture, email, Password, new TestTimeProvider());

        Assert.Equal(AuthenticationStatus.NoMembership, login.Status);
        Assert.Empty(login.MembershipTenantIds!);
        Assert.Null(login.SelectedTenantId);
    }

    [Fact]
    public async Task Registering_an_existing_address_looks_identical_and_changes_nothing()
    {
        var email = AuthTestSetup.NewEmail();
        var (accountId, _) = await AuthTestSetup.SeedAccountAsync(_fixture, email, "Original-Passw0rd-1");
        var freshResult = await RegisterAsync(AuthTestSetup.NewEmail(), "Someone", Password);

        var existingResult = await RegisterAsync(email, "Attacker Name", Password);

        Assert.Equal(freshResult, existingResult);
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM identity.account_credentials WHERE login_email_normalized = @e", ("e", EmailNormalizer.Normalize(email))));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM identity.accounts WHERE lower(email) = @e", ("e", EmailNormalizer.Normalize(email))));
        Assert.NotEqual(AuthenticationStatus.InvalidCredentials, (await AuthTestSetup.LoginAsync(_fixture, email, "Original-Passw0rd-1", new TestTimeProvider())).Status); // the original owner is unaffected
        Assert.Equal(AuthenticationStatus.InvalidCredentials, (await AuthTestSetup.LoginAsync(_fixture, email, Password, new TestTimeProvider())).Status); // and the "registrant" got nothing
        Assert.Equal("Seeded User", await DisplayNameAsync(accountId));
    }

    private async Task<string> DisplayNameAsync(long accountId)
    {
        await using var admin = _fixture.CreateAdminContext();
        return await admin.Accounts.Where(a => a.Id == accountId).Select(a => a.DisplayName).SingleAsync();
    }

    [Fact]
    public async Task Concurrent_registrations_of_one_address_create_one_account_and_all_look_accepted()
    {
        var email = AuthTestSetup.NewEmail();

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => RegisterAsync(email, "Racer", Password)));

        Assert.All(results, r => Assert.Equal(RegisterAccountStatus.Accepted, r.Status));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM identity.account_credentials WHERE login_email_normalized = @e", ("e", EmailNormalizer.Normalize(email))));
    }

    [Theory]
    [InlineData("not-an-email", "Name", Password, RegisterAccountStatus.InvalidEmail)]
    [InlineData("", "Name", Password, RegisterAccountStatus.InvalidEmail)]
    [InlineData("valid@example.com", "", Password, RegisterAccountStatus.InvalidDisplayName)]
    [InlineData("valid@example.com", "   ", Password, RegisterAccountStatus.InvalidDisplayName)]
    [InlineData("valid@example.com", "Name", "short", RegisterAccountStatus.PolicyViolation)]
    public async Task Invalid_input_is_rejected_and_creates_nothing(string email, string name, string password, RegisterAccountStatus expected)
    {
        var unique = email.Contains('@') ? $"{Guid.NewGuid():N}{email}" : email;
        var before = await CountAsync("SELECT count(*) FROM identity.accounts");

        var result = await RegisterAsync(unique, name, password);

        Assert.Equal(expected, result.Status);
        Assert.Equal(before, await CountAsync("SELECT count(*) FROM identity.accounts"));
    }

    [Fact]
    public async Task The_password_may_not_be_the_address_itself()
    {
        var email = AuthTestSetup.NewEmail();

        var result = await RegisterAsync(email, "Name", email);

        Assert.Equal(RegisterAccountStatus.PolicyViolation, result.Status);
        Assert.Contains("equals_email", result.PolicyViolations!);
    }

    [Fact]
    public async Task No_password_or_hash_reaches_the_audit_events()
    {
        var email = AuthTestSetup.NewEmail();
        await RegisterAsync(email, "Self Registered", Password);
        await RegisterAsync(email, "Self Registered", Password);

        await using var admin = _fixture.CreateAdminContext();
        var hash = await admin.AccountCredentials.Where(c => c.LoginEmailNormalized == EmailNormalizer.Normalize(email)).Select(c => c.PasswordHash).SingleAsync();
        var accountId = await admin.AccountCredentials.Where(c => c.LoginEmailNormalized == EmailNormalizer.Normalize(email)).Select(c => c.AccountId).SingleAsync();
        var events = await admin.AuthEvents.Where(e => e.EventType == "registration_created" && (e.AccountId == accountId || e.AccountId == null)).ToListAsync();
        Assert.Contains(events, e => e.AccountId == accountId && e.Outcome == "success");
        foreach (var e in events)
        {
            var text = $"{e.Outcome}|{e.Detail}|{e.CorrelationId}|{e.IpHash}";
            Assert.DoesNotContain(Password, text);
            Assert.DoesNotContain(hash, text);
            Assert.DoesNotContain(email, text, StringComparison.OrdinalIgnoreCase);
        }
    }
}
