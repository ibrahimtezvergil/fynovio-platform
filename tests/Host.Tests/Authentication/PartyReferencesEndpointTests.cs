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

    /// <summary>The fixture's database is shared by the tests of this class, which also register customers of their own;
    /// assertions about the SEED look only at the seeded ones.</summary>
    private static readonly HashSet<string> SeededTenantOne = ["Acme Corporation", "Globex Ltd", "Initech", "Ada Lovelace", "Grace Hopper", "Alan Turing"];

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
        Assert.Equal(new[] { "displayName", "email", "id", "partyType", "phone" }, person[0].EnumerateObject().Select(p => p.Name).Order().ToArray()); // display fields only, no internals
    }

    [Fact]
    public async Task A_blank_search_lists_alphabetically_and_take_limits_it()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var admin = await TokenAsync(client, DevSeeder.AdminEmail, 1);

        Assert.Equal(["Acme Corporation", "Ada Lovelace", "Alan Turing", "Globex Ltd", "Grace Hopper", "Initech"], Names(await OkAsync(client, admin, "?take=50")).Where(SeededTenantOne.Contains));
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

        Assert.Equal(6, Names(await OkAsync(client, admin, "?take=50")).Count(SeededTenantOne.Contains));
    }

    private static HttpRequestMessage Create(string token, object body, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/crm/references/parties") { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return request;
    }

    [Fact]
    public async Task A_sales_representative_registers_a_customer_who_is_then_found_and_usable_for_an_opportunity()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var rep = await TokenAsync(client, DevSeeder.SalesRepEmail);

        var created = await client.SendAsync(Create(rep, new { partyType = "person", name = "Katherine", surname = "Johnson", email = "kj@nasa.test" }));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetInt64();
        Assert.False(body.GetProperty("replayed").GetBoolean());
        Assert.Equal(["Katherine Johnson"], Names(await OkAsync(client, rep, "?search=johnson")));
        Assert.Equal(["Katherine Johnson"], Names(await OkAsync(client, rep, $"?ids={id}")));

        var opportunity = new HttpRequestMessage(HttpMethod.Post, "/opportunities") { Content = JsonContent.Create(new { partyId = id, currency = "EUR", estimatedAmount = 5 }) };
        opportunity.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rep);
        opportunity.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(opportunity)).StatusCode);
    }

    [Fact]
    public async Task A_retry_with_the_same_key_replays_and_the_same_key_with_another_body_is_a_conflict()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var rep = await TokenAsync(client, DevSeeder.SalesRepEmail);
        var key = Guid.NewGuid().ToString();

        var first = await (await client.SendAsync(Create(rep, new { partyType = "Organization", name = "Replay Co" }, key))).Content.ReadFromJsonAsync<JsonElement>();
        var second = await client.SendAsync(Create(rep, new { partyType = "Organization", name = "Replay Co" }, key));
        var different = await client.SendAsync(Create(rep, new { partyType = "Organization", name = "Another Co" }, key));

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var replay = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(first.GetProperty("id").GetInt64(), replay.GetProperty("id").GetInt64());
        Assert.True(replay.GetProperty("replayed").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, different.StatusCode);
        Assert.Equal("idempotency_key_reused", (await different.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString());
        Assert.Single(Names(await OkAsync(client, rep, "?search=replay%20co")));
    }

    [Fact]
    public async Task A_registered_customer_belongs_to_the_callers_tenant_only()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var one = await TokenAsync(client, DevSeeder.AdminEmail, 1);
        var two = await TokenAsync(client, DevSeeder.AdminEmail, 2);

        var created = await client.SendAsync(Create(one, new { partyType = "Organization", name = "Tenant One Only Ltd" }));
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        Assert.Equal(["Tenant One Only Ltd"], Names(await OkAsync(client, one, "?search=tenant%20one%20only")));
        Assert.Empty(Names(await OkAsync(client, two, "?search=tenant%20one%20only")));
        Assert.Empty(Names(await OkAsync(client, two, $"?ids={id}")));
    }

    [Fact]
    public async Task Only_members_holding_the_create_action_may_register_a_customer()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();

        foreach (var email in new[] { DevSeeder.ViewerEmail, DevSeeder.SingleTenantEmail })
        {
            var response = await client.SendAsync(Create(await TokenAsync(client, email), new { partyType = "Organization", name = "Should Not Exist" }));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        var admin = await TokenAsync(client, DevSeeder.AdminEmail, 1);
        Assert.Empty(Names(await OkAsync(client, admin, "?search=should%20not%20exist")));
        var anonymous = new HttpRequestMessage(HttpMethod.Post, "/crm/references/parties") { Content = JsonContent.Create(new { partyType = "Organization", name = "X" }) };
        anonymous.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(anonymous)).StatusCode);
    }

    [Theory]
    [InlineData("Organization", "", null)]
    [InlineData("Organization", "  ", null)]
    [InlineData("Company", "Acme", null)]
    [InlineData("99", "Acme", null)]
    [InlineData("Organization", "Acme", "Surname")]   // an organization has no surname
    public async Task Invalid_input_is_a_validation_error_and_creates_nothing(string partyType, string name, string? surname)
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var rep = await TokenAsync(client, DevSeeder.SalesRepEmail);
        var before = (await OkAsync(client, rep, "?take=50")).GetArrayLength();

        var response = await client.SendAsync(Create(rep, new { partyType, name, surname }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_error", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString());
        Assert.Equal(before, (await OkAsync(client, rep, "?take=50")).GetArrayLength());
    }
}
