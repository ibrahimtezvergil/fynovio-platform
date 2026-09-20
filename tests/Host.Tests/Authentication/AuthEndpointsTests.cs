using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Host.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Host.Tests.Authentication;

/// <summary>HTTP-level behaviour of the `/auth/*` endpoints against the real pipeline (JWT
/// validation, `ActorContextMiddleware`, CORS, rate limiting, CSRF guard) and a real PostgreSQL
/// with the unprivileged runtime role. Data is seeded through the real Access handlers.</summary>
[Collection(HostIntegrationCollection.Name)]
public sealed class AuthEndpointsTests : IClassFixture<AuthApiFixture>
{
    private const string CookieName = "fynovio_rt";
    private const string AllowedOrigin = "http://localhost:5173";

    private static readonly Dictionary<string, string?> SecureCookies = new()
    {
        ["Authentication__Session__AllowInsecureCookieInDevelopment"] = "false",
    };

    private readonly AuthApiFixture _fixture;

    public AuthEndpointsTests(AuthApiFixture fixture) => _fixture = fixture;

    // ---- helpers ---------------------------------------------------------------------------

    private static HttpRequestMessage Request(
        HttpMethod method,
        string path,
        object? body = null,
        string? cookie = null,
        bool csrfHeader = true,
        string? origin = null,
        string? fetchSite = null,
        string? bearer = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        if (csrfHeader)
            request.Headers.Add("X-Requested-With", "fynovio");
        if (cookie is not null)
            request.Headers.Add("Cookie", cookie);
        if (origin is not null)
            request.Headers.Add("Origin", origin);
        if (fetchSite is not null)
            request.Headers.Add("Sec-Fetch-Site", fetchSite);
        if (bearer is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return request;
    }

    private static Task<HttpResponseMessage> Login(HttpClient client, string email, string password) =>
        client.SendAsync(Request(HttpMethod.Post, "/auth/login", new { email, password }));

    private static Task<HttpResponseMessage> Refresh(HttpClient client, string cookie) =>
        client.SendAsync(Request(HttpMethod.Post, "/auth/refresh", cookie: cookie));

    private static Task<HttpResponseMessage> Logout(HttpClient client, string? cookie) =>
        client.SendAsync(Request(HttpMethod.Post, "/auth/logout", cookie: cookie));

    private static Task<HttpResponseMessage> SelectTenant(HttpClient client, string cookie, long tenantId) =>
        client.SendAsync(Request(HttpMethod.Post, "/auth/tenants/select", new { tenantId }, cookie));

    private static Task<HttpResponseMessage> Get(HttpClient client, string path, string? bearer) =>
        client.SendAsync(Request(HttpMethod.Get, path, csrfHeader: false, bearer: bearer));

    /// <summary>The `Set-Cookie` header for the refresh cookie, or null.</summary>
    private static string? SetCookieHeader(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(CookieName + "=", StringComparison.Ordinal))
            : null;

    /// <summary>`fynovio_rt=&lt;value&gt;` as a request `Cookie` header.</summary>
    private static string CookieHeader(HttpResponseMessage response)
    {
        var setCookie = SetCookieHeader(response) ?? throw new InvalidOperationException("No refresh cookie was set.");
        return setCookie.Split(';')[0];
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static JwtSecurityToken Decode(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    private static Guid SessionId(string token) => Guid.Parse(Decode(token).Claims.First(c => c.Type == "sid").Value);

    /// <summary>ProblemDetails carry a per-request `traceId`; everything else must be identical.</summary>
    private static async Task<string> StableBody(HttpResponseMessage response)
    {
        var node = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        node.Remove("traceId");
        return node.ToJsonString();
    }

    private static string MintToken(string key, string subject, long tenantId, Guid? sessionId, DateTime? expires = null, string issuer = JwtTestTokenFactory.Issuer)
    {
        var claims = new List<Claim> { new("sub", subject), new("tid", tenantId.ToString()) };
        if (sessionId is not null)
            claims.Add(new Claim("sid", sessionId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: JwtTestTokenFactory.Audience,
            claims: claims,
            notBefore: (expires ?? DateTime.UtcNow.AddHours(1)).AddHours(-2),
            expires: expires ?? DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ---- config ----------------------------------------------------------------------------

    [Fact]
    public async Task Config_exposes_the_password_policy_and_reports_self_registration_disabled()
    {
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();

        var response = await client.GetAsync("/auth/config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var body = await Json(response);
        Assert.False(body.GetProperty("selfRegistrationEnabled").GetBoolean());
        Assert.Equal(12, body.GetProperty("passwordPolicy").GetProperty("minLength").GetInt32());
        Assert.Equal(128, body.GetProperty("passwordPolicy").GetProperty("maxLength").GetInt32());
    }

    // ---- login -----------------------------------------------------------------------------

    [Fact]
    public async Task Login_with_one_membership_authenticates_and_sets_a_hardened_cookie()
    {
        var tenant = AuthApiFixture.NewTenantId();
        var user = await _fixture.SeedUserAsync(tenantIds: tenant);
        using var host = await _fixture.StartHostAsync(SecureCookies);
        using var client = host.CreateClient();

        // Different casing and surrounding whitespace than the stored email.
        var response = await Login(client, "  " + user.Email.ToUpperInvariant() + " ", AuthApiFixture.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var text = await response.Content.ReadAsStringAsync();
        var body = JsonDocument.Parse(text).RootElement;
        Assert.Equal("authenticated", body.GetProperty("status").GetString());
        Assert.Equal(600, body.GetProperty("expiresIn").GetInt32());
        Assert.Equal(user.Email, body.GetProperty("account").GetProperty("email").GetString()); // the stored email, not the submitted string
        Assert.Equal(user.AccountId, body.GetProperty("account").GetProperty("id").GetInt64());
        Assert.Equal(tenant, body.GetProperty("activeTenant").GetProperty("tenantId").GetInt64());
        Assert.Equal(tenant, body.GetProperty("memberships")[0].GetProperty("tenantId").GetInt64());

        var token = Decode(body.GetProperty("accessToken").GetString()!);
        Assert.Equal(AuthApiFixture.Issuer, token.Issuer);
        Assert.Equal(new[] { JwtTestTokenFactory.Audience }, token.Audiences.ToArray());
        Assert.Equal(user.Principal.Subject, token.Subject);
        Assert.Equal(tenant.ToString(), token.Claims.First(c => c.Type == "tid").Value);
        foreach (var claim in new[] { "sid", "jti", "iat", "exp" })
            Assert.Contains(token.Claims, c => c.Type == claim);

        var setCookie = SetCookieHeader(response)!;
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", setCookie, StringComparison.OrdinalIgnoreCase); // the configured CookiePath, not "/"
        Assert.DoesNotContain("domain=", setCookie, StringComparison.OrdinalIgnoreCase);
        var secret = setCookie.Split(';')[0].Split('=', 2)[1].Split('.')[1];
        Assert.DoesNotContain(secret, text); // the refresh secret is never in the body
    }

    [Fact]
    public async Task Login_with_several_memberships_requires_a_selection_and_the_selected_token_works()
    {
        var tenantA = AuthApiFixture.NewTenantId();
        var tenantB = AuthApiFixture.NewTenantId();
        var user = await _fixture.SeedUserAsync(tenantIds: [tenantA, tenantB]);
        await _fixture.GrantAsync(user.AccountId, tenantB, "crm.opportunity.list");
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();

        var login = await Login(client, user.Email, AuthApiFixture.Password);
        var loginBody = await Json(login);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal("tenant_selection_required", loginBody.GetProperty("status").GetString());
        Assert.False(loginBody.TryGetProperty("accessToken", out _));
        Assert.Equal(JsonValueKind.Null, loginBody.GetProperty("activeTenant").ValueKind);
        Assert.Equal(2, loginBody.GetProperty("memberships").GetArrayLength());

        var select = await SelectTenant(client, CookieHeader(login), tenantB);
        Assert.Equal(HttpStatusCode.OK, select.StatusCode);
        var selectBody = await Json(select);
        var token = selectBody.GetProperty("accessToken").GetString()!;
        Assert.Equal(tenantB.ToString(), Decode(token).Claims.First(c => c.Type == "tid").Value);
        Assert.Equal(user.Principal.Subject, Decode(token).Subject);

        // Accepted by the real pipeline on an existing Phase 2 route: 200 (the account holds crm.opportunity.list in tenantB).
        Assert.Equal(HttpStatusCode.OK, (await Get(client, "/opportunities", token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Get(client, "/auth/me", token)).StatusCode);
    }

    [Fact]
    public async Task Login_without_any_membership_reports_no_membership_and_issues_no_token()
    {
        var user = await _fixture.SeedUserAsync();
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();

        var response = await Login(client, user.Email, AuthApiFixture.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Json(response);
        Assert.Equal("no_membership", body.GetProperty("status").GetString());
        Assert.False(body.TryGetProperty("accessToken", out _));
        Assert.NotNull(SetCookieHeader(response));
    }

    [Fact]
    public async Task Login_failures_are_indistinguishable_for_unknown_wrong_locked_and_credentialless_accounts()
    {
        var tenant = AuthApiFixture.NewTenantId();
        var wrongPassword = await _fixture.SeedUserAsync(tenantIds: tenant);
        var locked = await _fixture.SeedUserAsync(tenantIds: tenant);
        var noCredential = await _fixture.SeedUserAsync(tenantIds: tenant);
        await _fixture.LockAccountAsync(locked.Email);
        await _fixture.RemoveCredentialAsync(noCredential.AccountId);
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();

        var attempts = new[]
        {
            await Login(client, AuthApiFixture.NewEmail(), AuthApiFixture.Password),
            await Login(client, wrongPassword.Email, "Definitely-Wrong-Password-1"),
            await Login(client, locked.Email, AuthApiFixture.Password), // correct password, but locked
            await Login(client, noCredential.Email, AuthApiFixture.Password),
        };

        Assert.All(attempts, r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
        Assert.All(attempts, r => Assert.Null(SetCookieHeader(r)));
        var bodies = new List<string>();
        foreach (var attempt in attempts)
            bodies.Add(await StableBody(attempt));
        Assert.Single(bodies.Distinct());
        Assert.Contains("invalid_credentials", bodies[0]);
    }

    [Theory]
    [InlineData(null, "Correct-Horse-Battery-9")]
    [InlineData("", "Correct-Horse-Battery-9")]
    [InlineData("someone@example.test", null)]
    [InlineData("someone@example.test", "")]
    public async Task Login_rejects_missing_or_empty_fields_with_field_errors(string? email, string? password)
    {
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();

        var response = await client.SendAsync(Request(HttpMethod.Post, "/auth/login", new { email, password }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await Json(response);
        Assert.Equal("validation_error", body.GetProperty("type").GetString());
        Assert.True(body.GetProperty("errors").EnumerateObject().Any());
    }

    [Fact]
    public async Task Login_rejects_oversized_input_malformed_json_and_wrong_content_type_without_echoing_the_password()
    {
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();
        var hugePassword = new string('p', 2000);

        var oversized = await client.SendAsync(Request(HttpMethod.Post, "/auth/login", new { email = new string('a', 300) + "@example.test", password = hugePassword }));
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
        Assert.DoesNotContain(hugePassword, await oversized.Content.ReadAsStringAsync());

        var malformed = Request(HttpMethod.Post, "/auth/login");
        malformed.Content = new StringContent("{ not json", Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(malformed)).StatusCode);

        var wrongType = Request(HttpMethod.Post, "/auth/login");
        wrongType.Content = new StringContent("email=a&password=b", Encoding.UTF8, "application/x-www-form-urlencoded");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(wrongType)).StatusCode);
    }

    [Fact]
    public async Task Login_records_a_keyed_user_agent_fingerprint_on_the_session_never_the_raw_header()
    {
        var user = await _fixture.SeedUserAsync(tenantIds: AuthApiFixture.NewTenantId());
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();
        const string userAgent = "FynovioTestBrowser/9.9 (integration-test)";
        var request = Request(HttpMethod.Post, "/auth/login", new { email = user.Email, password = AuthApiFixture.Password });
        request.Headers.Add("User-Agent", userAgent);

        var response = await client.SendAsync(request);

        var sessionId = SessionId((await Json(response)).GetProperty("accessToken").GetString()!);
        var stored = await _fixture.SessionUserAgentHashAsync(sessionId);
        var expected = new Host.Authentication.ClientFingerprint(new Host.Authentication.JwtOptions
        {
            Issuer = JwtTestTokenFactory.Issuer,
            Audience = JwtTestTokenFactory.Audience,
            SigningKey = JwtTestTokenFactory.SigningKey
        }).Hash(userAgent);
        Assert.NotNull(stored);
        Assert.Equal(expected, stored);
        Assert.DoesNotContain("FynovioTestBrowser", stored);
    }

    // ---- refresh ---------------------------------------------------------------------------

    [Fact]
    public async Task Refresh_rotates_the_cookie_and_returns_a_fresh_token()
    {
        var tenant = AuthApiFixture.NewTenantId();
        var user = await _fixture.SeedUserAsync(tenantIds: tenant);
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();
        var login = await Login(client, user.Email, AuthApiFixture.Password);
        var firstCookie = CookieHeader(login);

        var refresh = await Refresh(client, firstCookie);

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.NotEqual(firstCookie, CookieHeader(refresh));
        var body = await Json(refresh);
        Assert.Equal("authenticated", body.GetProperty("status").GetString());
        Assert.Equal(user.Email, body.GetProperty("account").GetProperty("email").GetString());
        Assert.Equal(tenant, body.GetProperty("activeTenant").GetProperty("tenantId").GetInt64());
        Assert.Equal(HttpStatusCode.OK, (await Get(client, "/auth/me", body.GetProperty("accessToken").GetString())).StatusCode);
    }

    [Fact]
    public async Task Refresh_without_a_cookie_is_401_session_invalid()
    {
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();

        var response = await client.SendAsync(Request(HttpMethod.Post, "/auth/refresh"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("session_invalid", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Replaying_a_rotated_cookie_revokes_the_whole_session_including_the_new_cookie()
    {
        var user = await _fixture.SeedUserAsync(tenantIds: AuthApiFixture.NewTenantId());
        var clock = new AdvanceableTimeProvider();
        using var host = await _fixture.StartHostAsync(configureServices: services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        });
        using var client = host.CreateClient();
        var login = await Login(client, user.Email, AuthApiFixture.Password);
        var sessionId = SessionId((await Json(login)).GetProperty("accessToken").GetString()!);
        var oldCookie = CookieHeader(login);
        var newCookie = CookieHeader(await Refresh(client, oldCookie));

        clock.Advance(TimeSpan.FromSeconds(3)); // past the 1s grace window

        var replay = await Refresh(client, oldCookie);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        var cleared = SetCookieHeader(replay)!;
        Assert.StartsWith(CookieName + "=;", cleared);
        Assert.Contains("expires=Thu, 01 Jan 1970", cleared, StringComparison.OrdinalIgnoreCase);
        Assert.Equal((true, "reuse_detected"), await _fixture.SessionStateAsync(sessionId));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(client, newCookie)).StatusCode);
    }

    [Fact]
    public async Task Two_concurrent_refreshes_with_the_same_cookie_never_both_succeed()
    {
        var user = await _fixture.SeedUserAsync(tenantIds: AuthApiFixture.NewTenantId());
        var clock = new AdvanceableTimeProvider(); // frozen: the loser lands inside the grace window
        using var host = await _fixture.StartHostAsync(configureServices: services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        });

        for (var round = 0; round < 5; round++)
        {
            using var client = host.CreateClient();
            var login = await Login(client, user.Email, AuthApiFixture.Password);
            var cookie = CookieHeader(login);
            var sessionId = SessionId((await Json(login)).GetProperty("accessToken").GetString()!);

            var responses = await Task.WhenAll(Refresh(client, cookie), Refresh(client, cookie));

            Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
            Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
            Assert.Equal(1, await _fixture.UnrotatedTokenCountAsync(sessionId));
        }
    }

    // ---- logout ----------------------------------------------------------------------------

    [Fact]
    public async Task Logout_clears_the_cookie_and_kills_the_previously_issued_access_token_immediately()
    {
        var tenant = AuthApiFixture.NewTenantId();
        var user = await _fixture.SeedUserAsync(tenantIds: tenant);
        await _fixture.GrantAsync(user.AccountId, tenant, "crm.opportunity.list");
        using var host = await _fixture.StartHostAsync(SecureCookies);
        using var client = host.CreateClient();
        var login = await Login(client, user.Email, AuthApiFixture.Password);
        var token = (await Json(login)).GetProperty("accessToken").GetString()!;
        var cookie = CookieHeader(login);
        Assert.Equal(HttpStatusCode.OK, (await Get(client, "/opportunities", token)).StatusCode); // works before

        var logout = await Logout(client, cookie);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        var cleared = SetCookieHeader(logout)!;
        Assert.StartsWith(CookieName + "=;", cleared);
        Assert.Contains("path=/api/auth", cleared, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cleared, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cleared, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cleared, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get(client, "/auth/me", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get(client, "/opportunities", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(client, cookie)).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("fynovio_rt=garbage")]
    [InlineData("fynovio_rt=00000000-0000-0000-0000-000000000000.nope")]
    public async Task Logout_without_a_usable_cookie_is_still_204(string? cookie)
    {
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();

        var response = await Logout(client, cookie);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.NotNull(SetCookieHeader(response));
    }

    // ---- me / 401 vs 403 -------------------------------------------------------------------

    [Fact]
    public async Task Me_returns_the_account_and_memberships_and_scopes_to_the_tokens_tenant()
    {
        var tenantA = AuthApiFixture.NewTenantId();
        var tenantB = AuthApiFixture.NewTenantId();
        var user = await _fixture.SeedUserAsync(tenantIds: [tenantA, tenantB]);
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();
        var cookie = CookieHeader(await Login(client, user.Email, AuthApiFixture.Password));
        var tokenA = (await Json(await SelectTenant(client, cookie, tenantA))).GetProperty("accessToken").GetString()!;
        var tokenB = (await Json(await SelectTenant(client, cookie, tenantB))).GetProperty("accessToken").GetString()!;

        var meA = await Json(await Get(client, "/auth/me", tokenA)); // an older token keeps its own tenant
        var meB = await Json(await Get(client, "/auth/me", tokenB));

        Assert.Equal(tenantA, meA.GetProperty("activeTenant").GetProperty("tenantId").GetInt64());
        Assert.Equal(tenantB, meB.GetProperty("activeTenant").GetProperty("tenantId").GetInt64());
        Assert.Equal(user.Email, meA.GetProperty("account").GetProperty("email").GetString());
        Assert.Equal(2, meA.GetProperty("memberships").GetArrayLength());
    }

    [Fact]
    public async Task Me_rejects_missing_forged_expired_sidless_and_revoked_tokens_with_401()
    {
        var tenant = AuthApiFixture.NewTenantId();
        var user = await _fixture.SeedUserAsync(tenantIds: tenant);
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();
        var login = await Login(client, user.Email, AuthApiFixture.Password);
        var validToken = (await Json(login)).GetProperty("accessToken").GetString()!;
        var sessionId = SessionId(validToken);
        Assert.Equal(HttpStatusCode.OK, (await Get(client, "/auth/me", validToken)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Get(client, "/auth/me", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get(client, "/auth/me", MintToken(new string('x', 40), user.Principal.Subject, tenant, sessionId))).StatusCode); // wrong key
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get(client, "/auth/me", MintToken(JwtTestTokenFactory.SigningKey, user.Principal.Subject, tenant, sessionId, expires: DateTime.UtcNow.AddMinutes(-5)))).StatusCode); // expired
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get(client, "/auth/me", MintToken(JwtTestTokenFactory.SigningKey, user.Principal.Subject, tenant, sessionId: null))).StatusCode); // no sid while it is required
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get(client, "/auth/me", MintToken(JwtTestTokenFactory.SigningKey, user.Principal.Subject, tenant, Guid.NewGuid()))).StatusCode); // unknown sid
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get(client, "/auth/me", MintToken(JwtTestTokenFactory.SigningKey, user.Principal.Subject, tenant, sessionId, issuer: "https://evil.example"))).StatusCode); // wrong issuer

        await Logout(client, CookieHeader(login));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get(client, "/auth/me", validToken)).StatusCode); // revoked sid
    }

    [Fact]
    public async Task A_token_for_a_tenant_the_account_has_left_is_403_not_401()
    {
        var tenant = AuthApiFixture.NewTenantId();
        var user = await _fixture.SeedUserAsync(tenantIds: tenant);
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();
        var token = (await Json(await Login(client, user.Email, AuthApiFixture.Password))).GetProperty("accessToken").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await Get(client, "/auth/me", token)).StatusCode);

        await _fixture.DisableMembershipAsync(user.AccountId, tenant);

        Assert.Equal(HttpStatusCode.Forbidden, (await Get(client, "/auth/me", token)).StatusCode);
    }

    [Fact]
    public async Task Authentication_and_authorization_failures_stay_distinct_401_vs_403()
    {
        var tenant = AuthApiFixture.NewTenantId();
        var user = await _fixture.SeedUserAsync(tenantIds: tenant); // member, but no CRM grant
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();
        var token = (await Json(await Login(client, user.Email, AuthApiFixture.Password))).GetProperty("accessToken").GetString()!;

        HttpRequestMessage CreateOpportunity(string? bearer)
        {
            var request = Request(HttpMethod.Post, "/opportunities", new { partyId = 1, currency = "EUR", estimatedAmount = 100 }, csrfHeader: false, bearer: bearer);
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            return request;
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(CreateOpportunity(null))).StatusCode); // no credentials
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(CreateOpportunity(token))).StatusCode); // authenticated, not permitted

        await _fixture.GrantAsync(user.AccountId, tenant, "crm.opportunity.create");
        var permitted = await client.SendAsync(CreateOpportunity(token));
        Assert.NotEqual(HttpStatusCode.Unauthorized, permitted.StatusCode); // past authentication and coarse authorization
        Assert.NotEqual(HttpStatusCode.Forbidden, permitted.StatusCode);
    }

    // ---- tenant selection ------------------------------------------------------------------

    [Fact]
    public async Task Selecting_an_unavailable_tenant_gives_the_same_403_for_unknown_disabled_invited_and_foreign_tenants()
    {
        var own = AuthApiFixture.NewTenantId();
        var disabled = AuthApiFixture.NewTenantId();
        var invited = AuthApiFixture.NewTenantId();
        var foreign = AuthApiFixture.NewTenantId();
        var user = await _fixture.SeedUserAsync(tenantIds: [own, disabled]);
        await _fixture.DisableMembershipAsync(user.AccountId, disabled);
        await _fixture.AddMembershipAsync(user.AccountId, invited, activate: false);
        var stranger = await _fixture.SeedUserAsync(tenantIds: foreign);
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();
        var cookie = CookieHeader(await Login(client, user.Email, AuthApiFixture.Password));

        var responses = new[]
        {
            await SelectTenant(client, cookie, AuthApiFixture.NewTenantId()), // unknown
            await SelectTenant(client, cookie, disabled),
            await SelectTenant(client, cookie, invited),
            await SelectTenant(client, cookie, foreign), // exists, but the account is not a member
        };

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
        var bodies = new List<string>();
        foreach (var response in responses)
            bodies.Add(await StableBody(response));
        Assert.Single(bodies.Distinct());
        Assert.Contains("tenant_not_permitted", bodies[0]);
        Assert.NotNull(stranger);
    }

    [Fact]
    public async Task Selecting_a_tenant_needs_a_valid_session_cookie()
    {
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();

        var none = await client.SendAsync(Request(HttpMethod.Post, "/auth/tenants/select", new { tenantId = 1 }));
        Assert.Equal(HttpStatusCode.Unauthorized, none.StatusCode);

        var garbage = await SelectTenant(client, CookieName + "=garbage", 1);
        Assert.Equal(HttpStatusCode.Unauthorized, garbage.StatusCode);
        Assert.StartsWith(CookieName + "=;", SetCookieHeader(garbage)!);

        var user = await _fixture.SeedUserAsync(tenantIds: AuthApiFixture.NewTenantId());
        var cookie = CookieHeader(await Login(client, user.Email, AuthApiFixture.Password));
        var missingId = await client.SendAsync(Request(HttpMethod.Post, "/auth/tenants/select", new { }, cookie));
        Assert.Equal(HttpStatusCode.BadRequest, missingId.StatusCode);
    }

    // ---- CSRF ------------------------------------------------------------------------------

    public static TheoryData<string> CookieAndCredentialEndpoints => new() { "/auth/login", "/auth/refresh", "/auth/logout", "/auth/tenants/select" };

    [Theory]
    [MemberData(nameof(CookieAndCredentialEndpoints))]
    public async Task State_changing_endpoints_reject_requests_without_the_custom_header(string path)
    {
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();

        var response = await client.SendAsync(Request(HttpMethod.Post, path, new { email = "a@b.c", password = "x", tenantId = 1 }, csrfHeader: false));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("csrf_rejected", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [MemberData(nameof(CookieAndCredentialEndpoints))]
    public async Task State_changing_endpoints_reject_a_foreign_origin_and_cross_site_fetches(string path)
    {
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();
        var body = new { email = "a@b.c", password = "x", tenantId = 1 };

        var foreignOrigin = await client.SendAsync(Request(HttpMethod.Post, path, body, origin: "https://evil.example"));
        var crossSite = await client.SendAsync(Request(HttpMethod.Post, path, body, fetchSite: "cross-site"));

        Assert.Equal(HttpStatusCode.Forbidden, foreignOrigin.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, crossSite.StatusCode);
        Assert.Contains("csrf_rejected", await foreignOrigin.Content.ReadAsStringAsync());
    }

    [Theory]
    [MemberData(nameof(CookieAndCredentialEndpoints))]
    public async Task State_changing_endpoints_accept_an_allow_listed_origin(string path)
    {
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();

        var response = await client.SendAsync(Request(HttpMethod.Post, path, new { email = "a@b.c", password = "x", tenantId = 1 }, origin: AllowedOrigin, fetchSite: "same-site"));

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode); // it got past the guard (401/204/400 depending on the endpoint)
    }

    // ---- CORS ------------------------------------------------------------------------------

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/auth/login");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,x-requested-with");
        return request;
    }

    [Fact]
    public async Task Cors_preflight_from_a_listed_origin_echoes_that_origin_with_credentials_never_a_wildcard()
    {
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();

        var allowed = await client.SendAsync(Preflight(AllowedOrigin));
        var unlisted = await client.SendAsync(Preflight("https://evil.example"));

        Assert.Equal(AllowedOrigin, allowed.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", allowed.Headers.GetValues("Access-Control-Allow-Credentials").Single());
        Assert.False(unlisted.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(unlisted.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Cors_is_off_entirely_when_no_origin_is_configured()
    {
        using var host = await _fixture.StartHostAsync(new Dictionary<string, string?> { ["Authentication__AllowedOrigins__0"] = "" });
        using var client = host.CreateClient();

        var response = await client.SendAsync(Preflight(AllowedOrigin));

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    // ---- rate limiting ---------------------------------------------------------------------

    [Fact]
    public async Task The_per_client_login_limit_answers_429_with_retry_after_and_a_problem_body()
    {
        using var host = await _fixture.StartHostAsync(new Dictionary<string, string?>
        {
            ["Authentication__RateLimiting__LoginPerMinute"] = "3",
            ["Authentication__RateLimiting__LoginPerIdentifierPerMinute"] = "1000",
        });
        using var client = host.CreateClient();

        for (var attempt = 0; attempt < 3; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await Login(client, AuthApiFixture.NewEmail(), "whatever-password-1")).StatusCode);
        var limited = await Login(client, AuthApiFixture.NewEmail(), "whatever-password-1");

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("Retry-After"));
        Assert.Contains("rate_limited", await limited.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_per_identifier_login_limit_applies_across_attempts_but_not_to_other_identifiers()
    {
        using var host = await _fixture.StartHostAsync(new Dictionary<string, string?>
        {
            ["Authentication__RateLimiting__LoginPerMinute"] = "1000",
            ["Authentication__RateLimiting__LoginPerIdentifierPerMinute"] = "2",
        });
        using var client = host.CreateClient();
        var email = AuthApiFixture.NewEmail();

        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(client, email, "whatever-password-1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(client, email.ToUpperInvariant(), "whatever-password-1")).StatusCode); // same identifier after normalisation
        var limited = await Login(client, email, "whatever-password-1");
        var other = await Login(client, AuthApiFixture.NewEmail(), "whatever-password-1");

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("Retry-After"));
        Assert.Contains("rate_limited", await limited.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, other.StatusCode);
    }

    // ---- logs ------------------------------------------------------------------------------

    [Fact]
    public async Task No_secret_reaches_the_logs_during_login_refresh_logout_and_a_failed_login()
    {
        var user = await _fixture.SeedUserAsync(tenantIds: AuthApiFixture.NewTenantId());
        var logs = new CapturingLoggerProvider();
        using var host = await _fixture.StartHostAsync(logProvider: logs);
        using var client = host.CreateClient();
        const string wrongPassword = "Distinctive-Wrong-Password-For-Logs-77";

        await Login(client, user.Email, wrongPassword);
        var login = await Login(client, user.Email, AuthApiFixture.Password);
        var accessToken = (await Json(login)).GetProperty("accessToken").GetString()!;
        var firstCookie = CookieHeader(login);
        var refresh = await Refresh(client, firstCookie);
        var secondCookie = CookieHeader(refresh);
        await Logout(client, secondCookie);

        var everything = string.Join('\n', logs.Entries);
        Assert.NotEmpty(logs.Entries);
        Assert.DoesNotContain(AuthApiFixture.Password, everything);
        Assert.DoesNotContain(wrongPassword, everything);
        Assert.DoesNotContain(accessToken, everything);
        Assert.DoesNotContain(Decode(accessToken).RawSignature, everything);
        foreach (var cookie in new[] { firstCookie, secondCookie })
        {
            var value = cookie.Split('=', 2)[1];
            Assert.DoesNotContain(value, everything);
            Assert.DoesNotContain(value.Split('.')[1], everything);
        }
    }
}
