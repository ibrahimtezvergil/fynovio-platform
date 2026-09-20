using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Host.Authentication;
using Host.Tests.Fixtures;
using Xunit;

namespace Host.Tests.Authentication;

/// <summary>Phase 2.6 OD1 through the real stack (seeded identities, real PDP, RLS as the runtime role): the UI may
/// offer only members Access says are permitted, and the server re-checks the target it is sent.</summary>
[Collection(HostIntegrationCollection.Name)]
public sealed class AssignablePrincipalsEndpointTests : IClassFixture<AuthApiFixture>
{
    private const string SeedPassword = "Seed-Test-Passw0rd-1";

    private static readonly Dictionary<string, string?> SeedEnabled = new()
    {
        ["DevSeed__Enabled"] = "true",
        ["DevSeed__Password"] = SeedPassword,
    };

    private readonly AuthApiFixture _fixture;

    public AssignablePrincipalsEndpointTests(AuthApiFixture fixture) => _fixture = fixture;

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }

        return request;
    }

    private static async Task<(JsonElement Body, string Cookie)> LoginAsync(HttpClient client, string email)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login") { Content = JsonContent.Create(new { email, password = SeedPassword }) };
        request.Headers.Add("X-Requested-With", "fynovio");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = response.Headers.GetValues("Set-Cookie").First(v => v.StartsWith("fynovio_rt=", StringComparison.Ordinal)).Split(';')[0];
        return (await response.Content.ReadFromJsonAsync<JsonElement>(), cookie);
    }

    private static async Task<string> TokenAsync(HttpClient client, string email, long? tenantId = null)
    {
        var (body, cookie) = await LoginAsync(client, email);
        if (tenantId is null)
            return body.GetProperty("accessToken").GetString()!;

        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/tenants/select") { Content = JsonContent.Create(new { tenantId }) };
        request.Headers.Add("X-Requested-With", "fynovio");
        request.Headers.Add("Cookie", cookie);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }

    private static async Task<(long Id, long Version)> CreateAsync(HttpClient client, string token)
    {
        var created = await client.SendAsync(Authorized(HttpMethod.Post, "/opportunities", token, new { partyId = 1, currency = "EUR", estimatedAmount = 10 }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("opportunityId").GetInt64();
        var detail = await (await client.SendAsync(Authorized(HttpMethod.Get, $"/opportunities/{id}", token))).Content.ReadFromJsonAsync<JsonElement>();
        return (id, detail.GetProperty("rowVersion").GetInt64());
    }

    private static async Task<JsonElement> AssignableAsync(HttpClient client, string token, long id, string query = "")
    {
        var response = await client.SendAsync(Authorized(HttpMethod.Get, $"/opportunities/{id}/assignable-principals{query}", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static string[] Names(JsonElement list) => list.EnumerateArray().Select(e => e.GetProperty("displayName").GetString()!).ToArray();

    [Fact]
    public async Task Lists_only_members_permitted_to_hold_the_opportunity_and_never_the_owner()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var admin = await TokenAsync(client, DevSeeder.AdminEmail, 1);
        var (id, _) = await CreateAsync(client, admin);

        var list = await AssignableAsync(client, admin, id);

        // Sales rep: read + change_stage. Not listed: the owner (admin), the read-only viewer, the member with no grants.
        Assert.Equal(["Dev Sales Representative"], Names(list));
        var rep = list[0];
        Assert.False(string.IsNullOrEmpty(rep.GetProperty("issuer").GetString()));
        Assert.False(string.IsNullOrEmpty(rep.GetProperty("subject").GetString()));
        Assert.Equal(DevSeeder.SalesRepEmail, rep.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Search_narrows_the_list_and_a_miss_is_empty()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var admin = await TokenAsync(client, DevSeeder.AdminEmail, 1);
        var (id, _) = await CreateAsync(client, admin);

        Assert.Equal(["Dev Sales Representative"], Names(await AssignableAsync(client, admin, id, "?search=SALES&take=5")));
        Assert.Empty(Names(await AssignableAsync(client, admin, id, "?search=zzz-nobody")));
        Assert.Empty(Names(await AssignableAsync(client, admin, id, "?search=%25"))); // a literal percent sign, not a wildcard
    }

    [Fact]
    public async Task Only_an_actor_who_may_reassign_that_record_gets_an_answer()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var admin = await TokenAsync(client, DevSeeder.AdminEmail, 1);
        var (id, _) = await CreateAsync(client, admin);

        foreach (var email in new[] { DevSeeder.ViewerEmail, DevSeeder.SalesRepEmail })
        {
            var token = await TokenAsync(client, email);
            var response = await client.SendAsync(Authorized(HttpMethod.Get, $"/opportunities/{id}/assignable-principals", token));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("forbidden", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString());
        }

        var noGrants = await TokenAsync(client, DevSeeder.SingleTenantEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Authorized(HttpMethod.Get, $"/opportunities/{id}/assignable-principals", noGrants))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(Authorized(HttpMethod.Get, "/opportunities/999999/assignable-principals", admin))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/opportunities/{id}/assignable-principals")).StatusCode);
    }

    [Fact]
    public async Task The_server_refuses_a_reassignment_target_that_is_not_assignable_however_it_was_chosen()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var admin = await TokenAsync(client, DevSeeder.AdminEmail, 1);
        var (id, version) = await CreateAsync(client, admin);
        var issuer = (await AssignableAsync(client, admin, id))[0].GetProperty("issuer").GetString()!;
        var repSubject = (await AssignableAsync(client, admin, id))[0].GetProperty("subject").GetString()!;

        // A read-only member, a no-grant member, and a principal nobody has heard of.
        var viewerSubject = await SubjectOfAsync(client, DevSeeder.ViewerEmail, issuer);
        var singleSubject = await SubjectOfAsync(client, DevSeeder.SingleTenantEmail, issuer);
        foreach (var (targetIssuer, targetSubject) in new[] { (issuer, viewerSubject), (issuer, singleSubject), (issuer, "ghost"), ("elsewhere", repSubject) })
        {
            var refused = await client.SendAsync(Authorized(HttpMethod.Post, $"/opportunities/{id}/reassign", admin,
                new { expectedVersion = version, newPrincipalIssuer = targetIssuer, newPrincipalSubject = targetSubject }));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
            Assert.Equal("principal_not_assignable", (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString());
        }

        var detail = await (await client.SendAsync(Authorized(HttpMethod.Get, $"/opportunities/{id}", admin))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(version, detail.GetProperty("rowVersion").GetInt64()); // nothing changed
    }

    [Fact]
    public async Task Reassigning_to_a_listed_member_hands_the_opportunity_over()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var admin = await TokenAsync(client, DevSeeder.AdminEmail, 1);
        var (id, version) = await CreateAsync(client, admin);
        var rep = (await AssignableAsync(client, admin, id))[0];

        var reassigned = await client.SendAsync(Authorized(HttpMethod.Post, $"/opportunities/{id}/reassign", admin,
            new { expectedVersion = version, newPrincipalIssuer = rep.GetProperty("issuer").GetString(), newPrincipalSubject = rep.GetProperty("subject").GetString() }));
        Assert.Equal(HttpStatusCode.OK, reassigned.StatusCode);

        // The new owner works it (canOpen: it is still a draft) and the previous owner is now a candidate again.
        var repToken = await TokenAsync(client, DevSeeder.SalesRepEmail);
        var actions = await (await client.SendAsync(Authorized(HttpMethod.Get, $"/opportunities/{id}/actions", repToken))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(actions.GetProperty("canOpen").GetBoolean());
        Assert.Equal(["Dev Admin"], Names(await AssignableAsync(client, admin, id)));
    }

    [Fact]
    public async Task A_closed_opportunity_has_nobody_assignable()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var admin = await TokenAsync(client, DevSeeder.AdminEmail, 1);
        var (id, version) = await CreateAsync(client, admin);
        var lost = await client.SendAsync(Authorized(HttpMethod.Post, $"/opportunities/{id}/lose", admin, new { expectedVersion = version, lostReason = "n/a" }));
        Assert.Equal(HttpStatusCode.OK, lost.StatusCode);

        Assert.Empty(Names(await AssignableAsync(client, admin, id)));
    }

    [Fact]
    public async Task Tenant_two_offers_nobody_because_its_only_member_owns_the_record()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var adminTwo = await TokenAsync(client, DevSeeder.AdminEmail, 2);
        var (id, _) = await CreateAsync(client, adminTwo);

        Assert.Empty(Names(await AssignableAsync(client, adminTwo, id))); // tenant 1's rep is not a member here
    }

    /// <summary>The platform-issuer subject of a seeded account, read from its token (`sub`).</summary>
    private static async Task<string> SubjectOfAsync(HttpClient client, string email, string issuer)
    {
        var token = await TokenAsync(client, email);
        var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        using var json = JsonDocument.Parse(Convert.FromBase64String(payload));
        Assert.Equal(issuer, json.RootElement.GetProperty("iss").GetString());
        return json.RootElement.GetProperty("sub").GetString()!;
    }
}
