using System.Net;
using Host.Tests.Fixtures;
using Xunit;
using static Host.Tests.Authentication.LifecycleTestSupport;

namespace Host.Tests.Authentication;

/// <summary>Forgot / reset / change password and self-registration over HTTP.</summary>
[Collection(HostIntegrationCollection.Name)]
public sealed class PasswordEndpointsTests : IClassFixture<AuthApiFixture>
{
    private readonly AuthApiFixture _fixture;

    public PasswordEndpointsTests(AuthApiFixture fixture) => _fixture = fixture;

    // ---- forgot ----------------------------------------------------------------------------

    [Fact]
    public async Task Forgot_answers_202_with_the_same_body_for_a_known_and_an_unknown_address_and_mails_only_the_known_one()
    {
        var known = await _fixture.SeedUserAsync(tenantIds: AuthApiFixture.NewTenantId());
        var unknownEmail = AuthApiFixture.NewEmail();
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();

        var knownResponse = await Forgot(client, known.Email);
        var unknownResponse = await Forgot(client, unknownEmail);

        Assert.Equal(HttpStatusCode.Accepted, knownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknownResponse.StatusCode);
        Assert.Equal(await StableBody(knownResponse), await StableBody(unknownResponse));
        Assert.Equal("no-store", knownResponse.Headers.CacheControl?.ToString());
        Assert.Single(await MailboxAsync(client, known.Email));
        Assert.Empty(await MailboxAsync(client, unknownEmail));
    }

    [Fact]
    public async Task Forgot_beyond_the_per_address_budget_stays_202_but_sends_nothing_more()
    {
        var user = await _fixture.SeedUserAsync(tenantIds: AuthApiFixture.NewTenantId());
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Accepted, (await Forgot(client, user.Email)).StatusCode);

        Assert.Equal(3, (await MailboxAsync(client, user.Email)).Count); // the default budget is 3 per address per hour
    }

    [Fact]
    public async Task Forgot_is_rate_limited_per_client_and_guarded_against_csrf()
    {
        using var host = await _fixture.StartHostAsync(new() { ["Authentication__RateLimiting__ForgotPerHour"] = "3" });
        using var client = host.CreateClient();

        // The limiter runs before the endpoint's own checks, so refused requests spend permits too.
        var noHeader = await client.SendAsync(Post("/auth/password/forgot", new { email = "a@example.test" }, csrfHeader: false));
        Assert.Equal(HttpStatusCode.Forbidden, noHeader.StatusCode);
        var missing = await client.SendAsync(Post("/auth/password/forgot", new { email = "" }));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        Assert.Equal(HttpStatusCode.Accepted, (await Forgot(client, "a@example.test")).StatusCode);
        var limited = await Forgot(client, "b@example.test");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("Retry-After"));
    }

    // ---- reset -----------------------------------------------------------------------------

    [Fact]
    public async Task Reset_sets_the_password_signs_every_session_out_and_the_link_works_once()
    {
        var user = await _fixture.SeedUserAsync(tenantIds: AuthApiFixture.NewTenantId());
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();
        var oldSession = CookieHeader(await Login(client, user.Email, AuthApiFixture.Password));
        await Forgot(client, user.Email);
        var mail = await MailAsync(client, user.Email, "password_reset");
        Assert.StartsWith($"{AllowedOrigin}/reset-password#token=", mail.Link);

        var reset = await Reset(client, mail.Token, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Contains("expires=Thu, 01 Jan 1970", SetCookieHeader(reset)!, StringComparison.OrdinalIgnoreCase); // the browser's own cookie is cleared
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(client, user.Email, AuthApiFixture.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Login(client, user.Email, NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(client, oldSession)).StatusCode); // the pre-reset session is dead

        var replay = await Reset(client, mail.Token, "Another-Passw0rd-Entirely-7");
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal("invalid_or_expired_token", await ProblemType(replay));
    }

    [Fact]
    public async Task Reset_refuses_an_expired_link_and_a_weak_password_without_burning_the_link()
    {
        var user = await _fixture.SeedUserAsync(tenantIds: AuthApiFixture.NewTenantId());
        var clock = new AdvanceableTimeProvider();
        using var host = await StartWithClockAsync(_fixture, clock);
        using var client = host.CreateClient();

        await Forgot(client, user.Email);
        var first = await MailAsync(client, user.Email, "password_reset");
        var weak = await Reset(client, first.Token, "short");
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Equal("password_policy_violation", await ProblemType(weak));
        Assert.Equal(HttpStatusCode.NoContent, (await Reset(client, first.Token, NewPassword)).StatusCode); // still usable

        await Forgot(client, user.Email);
        var late = (await MailboxAsync(client, user.Email)).First(m => m.Token != first.Token);
        clock.Advance(TimeSpan.FromMinutes(31)); // the default lifetime is 30 minutes
        var expired = await Reset(client, late.Token, "Yet-Another-Passw0rd-9");
        Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);
        Assert.Equal("invalid_or_expired_token", await ProblemType(expired));
    }

    [Fact]
    public async Task Reset_needs_the_csrf_header()
    {
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();

        var response = await client.SendAsync(Post("/auth/password/reset", new { token = "a.b", newPassword = NewPassword }, csrfHeader: false));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- change ----------------------------------------------------------------------------

    [Fact]
    public async Task Change_keeps_this_session_and_signs_every_other_session_out()
    {
        var user = await _fixture.SeedUserAsync(tenantIds: AuthApiFixture.NewTenantId());
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();
        var otherDevice = CookieHeader(await Login(client, user.Email, AuthApiFixture.Password));
        var thisLogin = await Login(client, user.Email, AuthApiFixture.Password);
        var thisCookie = CookieHeader(thisLogin);
        var thisToken = (await Json(thisLogin)).GetProperty("accessToken").GetString()!;

        var change = await Change(client, thisToken, AuthApiFixture.Password, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(client, otherDevice)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Refresh(client, thisCookie)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Get("/auth/me", thisToken))).StatusCode); // the caller's own token keeps working
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(client, user.Email, AuthApiFixture.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Login(client, user.Email, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task A_wrong_current_password_is_400_never_401_so_the_client_does_not_read_it_as_an_expired_session()
    {
        var user = await _fixture.SeedUserAsync(tenantIds: AuthApiFixture.NewTenantId());
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();
        var token = await AccessTokenAsync(client, user.Email, AuthApiFixture.Password);

        var wrong = await Change(client, token, "Not-The-Current-Passw0rd-1", NewPassword);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal("invalid_current_password", await ProblemType(wrong));

        var weak = await Change(client, token, AuthApiFixture.Password, "short");
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Equal("password_policy_violation", await ProblemType(weak));

        Assert.Equal(HttpStatusCode.OK, (await Login(client, user.Email, AuthApiFixture.Password)).StatusCode); // nothing changed
    }

    [Fact]
    public async Task Change_requires_a_bearer_token_and_the_csrf_header()
    {
        var user = await _fixture.SeedUserAsync(tenantIds: AuthApiFixture.NewTenantId());
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();
        var token = await AccessTokenAsync(client, user.Email, AuthApiFixture.Password);

        var anonymous = await client.SendAsync(Post("/auth/password/change", new { currentPassword = AuthApiFixture.Password, newPassword = NewPassword }));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var noHeader = await client.SendAsync(Post("/auth/password/change", new { currentPassword = AuthApiFixture.Password, newPassword = NewPassword }, token, csrfHeader: false));
        Assert.Equal(HttpStatusCode.Forbidden, noHeader.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await Login(client, user.Email, AuthApiFixture.Password)).StatusCode);
    }

    // ---- registration ----------------------------------------------------------------------

    [Fact]
    public async Task Registration_is_off_by_default_the_route_does_not_exist()
    {
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();

        var response = await client.SendAsync(Post("/auth/register", new { email = AuthApiFixture.NewEmail(), displayName = "Nobody", password = NewPassword }));
        var config = await Json(await client.GetAsync("/auth/config"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(config.GetProperty("selfRegistrationEnabled").GetBoolean());
    }

    [Fact]
    public async Task Registration_when_enabled_creates_an_identity_with_no_tenant_access_and_answers_neutrally()
    {
        using var host = await _fixture.StartHostAsync(new() { ["Authentication__SelfRegistration__Enabled"] = "true" });
        using var client = host.CreateClient();
        var email = AuthApiFixture.NewEmail();

        var first = await client.SendAsync(Post("/auth/register", new { email, displayName = "Self Registered", password = NewPassword }));
        var duplicate = await client.SendAsync(Post("/auth/register", new { email, displayName = "Someone Else", password = "Other-Passw0rd-Entirely-3" }));

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, duplicate.StatusCode);
        Assert.Equal(await StableBody(first), await StableBody(duplicate)); // an existing address is indistinguishable
        Assert.True((await Json(await client.GetAsync("/auth/config"))).GetProperty("selfRegistrationEnabled").GetBoolean());

        var login = await Json(await Login(client, email, NewPassword));
        Assert.Equal("no_membership", login.GetProperty("status").GetString()); // registering grants nothing
        Assert.False(login.TryGetProperty("accessToken", out _));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(client, email, "Other-Passw0rd-Entirely-3")).StatusCode); // the first registration keeps the address

        var weak = await client.SendAsync(Post("/auth/register", new { email = AuthApiFixture.NewEmail(), displayName = "Weak", password = "short" }));
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        var noHeader = await client.SendAsync(Post("/auth/register", new { email = AuthApiFixture.NewEmail(), displayName = "No Header", password = NewPassword }, csrfHeader: false));
        Assert.Equal(HttpStatusCode.Forbidden, noHeader.StatusCode);
    }

    [Fact]
    public async Task Enabling_registration_outside_development_needs_an_explicit_acknowledgement()
    {
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            using var host = await _fixture.StartHostAsync(Production(new() { ["Authentication__SelfRegistration__Enabled"] = "true" }));
        });

        using var acknowledged = await _fixture.StartHostAsync(Production(new()
        {
            ["Authentication__SelfRegistration__Enabled"] = "true",
            ["Authentication__SelfRegistration__AcknowledgeUnverifiedEmail"] = "true",
        }));
    }
}
