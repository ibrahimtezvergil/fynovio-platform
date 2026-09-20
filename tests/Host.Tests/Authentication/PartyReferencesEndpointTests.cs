using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Host.Authentication;
using Host.Tests.Fixtures;
using Xunit;

namespace Host.Tests.Authentication;

/// <summary>Phase 2.6 G2 through the real stack: seeded Parties, the real PDP, MasterData under RLS as the runtime role.</summary>
[Collection(HostIntegrationCollection.Name)]
public sealed class PartyReferencesEndpointTests : IClassFixture<AuthApiFixture>
{
    private const string SeedPassword = "Seed-Test-Passw0rd-1";

    private static readonly Dictionary<string, string?> SeedEnabled = new()
    {
        ["DevSeed__Enabled"] = "true",
        ["DevSeed__Password"] = SeedPassword,
    };

    private readonly AuthApiFixture _fixture;

    public PartyReferencesEndpointTests(AuthApiFixture fixture) => _fixture = fixture;

    private static async Task<string> TokenAsync(HttpClient client, string email, long? tenantId = null)
    {
        var login = new HttpRequestMessage(HttpMethod.Post, "/auth/login") { Content = JsonContent.Create(new { email, password = SeedPassword }) };
        login.Headers.Add("X-Requested-With", "fynovio");
        var response = await client.SendAsync(login);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (tenantId is null)
            return body.GetProperty("accessToken").GetString()!;

        var cookie = response.Headers.GetValues("Set-Cookie").First(v => v.StartsWith("fynovio_rt=", StringComparison.Ordinal)).Split(';')[0];
        var select = new HttpRequestMessage(HttpMethod.Post, "/auth/tenants/select") { Content = JsonContent.Create(new { tenantId }) };
        select.Headers.Add("X-Requested-With", "fynovio");
        select.Headers.Add("Cookie", cookie);
        var selected = await client.SendAsync(select);
        Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
        return (await selected.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string token, string query)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/crm/references/parties" + query);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> OkAsync(HttpClient client, string token, string query)
    {
        var response = await GetAsync(client, token, query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static string[] Names(JsonElement list) => list.EnumerateArray().Select(e => e.GetProperty("displayName").GetString()!).ToArray();

    [Fact]
    public async Task Search_finds_the_tenants_parties_and_returns_display_fields_only()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var admin = await TokenAsync(client, DevSeeder.AdminEmail, 1);

        var acme = await OkAsync(client, admin, "?search=acme");
        var person = await OkAsync(client, admin, "?search=lovelace");

        Assert.Equal(["Acme Corporation"], Names(acme));
        Assert.Equal("Organization", acme[0].GetProperty("partyType").GetString());
        Assert.Equal(["Ada Lovelace"], Names(person));
        Assert.Equal("Person", person[0].GetProperty("partyType").GetString());
        Assert.Equal(new[] { "displayName", "email", "id", "partyType" }, person[0].EnumerateObject().Select(p => p.Name).Order().ToArray()); // no phone, no internals
    }

    [Fact]
    public async Task A_blank_search_lists_alphabetically_and_take_limits_it()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var admin = await TokenAsync(client, DevSeeder.AdminEmail, 1);

        Assert.Equal(["Acme Corporation", "Ada Lovelace", "Alan Turing", "Globex Ltd", "Grace Hopper", "Initech"], Names(await OkAsync(client, admin, "")));
        Assert.Equal(["Acme Corporation", "Ada Lovelace"], Names(await OkAsync(client, admin, "?take=2")));
        Assert.Empty(Names(await OkAsync(client, admin, "?search=%25")));   // a literal percent, not "everything"
    }

    [Fact]
    public async Task Each_tenant_only_ever_sees_its_own_parties()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var one = await TokenAsync(client, DevSeeder.AdminEmail, 1);
        var two = await TokenAsync(client, DevSeeder.AdminEmail, 2);

        Assert.Empty(Names(await OkAsync(client, two, "?search=acme")));
        Assert.Equal(["Umbrella Holdings"], Names(await OkAsync(client, two, "?search=umbrella")));
        Assert.Empty(Names(await OkAsync(client, one, "?search=umbrella")));
        Assert.Equal(3, (await OkAsync(client, two, "")).GetArrayLength());
    }

    [Fact]
    public async Task Ids_resolve_display_names_and_never_cross_tenants()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var one = await TokenAsync(client, DevSeeder.AdminEmail, 1);
        var two = await TokenAsync(client, DevSeeder.AdminEmail, 2);
        var acme = (await OkAsync(client, one, "?search=acme"))[0].GetProperty("id").GetInt64();
        var umbrella = (await OkAsync(client, two, "?search=umbrella"))[0].GetProperty("id").GetInt64();

        Assert.Equal(["Acme Corporation"], Names(await OkAsync(client, one, $"?ids={acme}")));
        Assert.Empty(Names(await OkAsync(client, one, $"?ids={umbrella},999999"))); // another tenant's id and a missing one: absent
        Assert.Empty(Names(await OkAsync(client, two, $"?ids={acme}")));
    }

    [Theory]
    [InlineData("?ids=abc")]
    [InlineData("?ids=1,,x")]
    [InlineData("?ids=0")]
    [InlineData("?ids=-3")]
    [InlineData("?ids=1e3")]
    public async Task Malformed_ids_are_a_validation_error_not_a_partial_answer(string query)
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var admin = await TokenAsync(client, DevSeeder.AdminEmail, 1);

        var response = await GetAsync(client, admin, query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_error", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString());
    }

    [Fact]
    public async Task Only_members_holding_the_search_action_may_read_parties()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await GetAsync(client, await TokenAsync(client, DevSeeder.SalesRepEmail), "?search=acme")).StatusCode);

        foreach (var email in new[] { DevSeeder.ViewerEmail, DevSeeder.SingleTenantEmail })
        {
            var response = await GetAsync(client, await TokenAsync(client, email), "?search=acme");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.DoesNotContain("Acme", await response.Content.ReadAsStringAsync());
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/crm/references/parties?search=acme")).StatusCode);
    }

    [Fact]
    public async Task Seeding_twice_never_duplicates_parties()
    {
        using (await _fixture.StartHostAsync(SeedEnabled)) { }
        using var second = await _fixture.StartHostAsync(SeedEnabled);
        using var client = second.CreateClient();
        var admin = await TokenAsync(client, DevSeeder.AdminEmail, 1);

        Assert.Equal(6, (await OkAsync(client, admin, "")).GetArrayLength());
    }
}
