using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Host.Authentication;
using Host.Tests.Fixtures;
using Xunit;

namespace Host.Tests.Authentication;

/// <summary>Tier-1 custom fields through the real stack (seeded identities, real PDP, CRM template v3, RLS as the
/// runtime role): definitions under /crm/settings/custom-fields, values on /opportunities.</summary>
[Collection(HostIntegrationCollection.Name)]
public sealed class CustomFieldsEndpointTests : IClassFixture<AuthApiFixture>
{
    private const string SeedPassword = "Seed-Test-Passw0rd-1";

    private static readonly Dictionary<string, string?> SeedEnabled = new()
    {
        ["DevSeed__Enabled"] = "true",
        ["DevSeed__Password"] = SeedPassword,
    };

    private readonly AuthApiFixture _fixture;

    public CustomFieldsEndpointTests(AuthApiFixture fixture) => _fixture = fixture;

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token, object? body = null, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
            request.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString());
        }

        return request;
    }

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

    private static string UniqueKey(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..20];

    private static async Task<(long Id, long RowVersion)> DefineAsync(HttpClient client, string token, object definition)
    {
        var response = await client.SendAsync(Authorized(HttpMethod.Post, "/crm/settings/custom-fields", token, definition));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("definitionId").GetInt64(), body.GetProperty("rowVersion").GetInt64());
    }

    private static async Task<long> SeededPartyIdAsync(HttpClient client, string token)
    {
        var parties = await (await client.SendAsync(Authorized(HttpMethod.Get, "/crm/references/parties?take=1", token))).Content.ReadFromJsonAsync<JsonElement>();
        return parties[0].GetProperty("id").GetInt64();
    }

    private static async Task<JsonElement> DetailAsync(HttpClient client, string token, long id) =>
        await (await client.SendAsync(Authorized(HttpMethod.Get, $"/opportunities/{id}", token))).Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task A_defined_field_is_listed_written_on_create_and_update_and_returned_on_detail()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var admin = await TokenAsync(client, DevSeeder.AdminEmail, 1);
        var key = UniqueKey("region");
        await DefineAsync(client, admin, new
        {
            key, label = "Region", type = "select", isRequired = false, sortOrder = 1,
            config = new { options = new[] { new { key = "north", label = "North" }, new { key = "south", label = "South" } } }
        });

        var list = await (await client.SendAsync(Authorized(HttpMethod.Get, "/crm/settings/custom-fields", admin))).Content.ReadFromJsonAsync<JsonElement>();
        var listed = list.EnumerateArray().Single(d => d.GetProperty("fieldName").GetString() == key);
        Assert.Equal("select", listed.GetProperty("fieldType").GetString());
        Assert.Equal("Active", listed.GetProperty("status").GetString());

        var partyId = await SeededPartyIdAsync(client, admin);
        var created = await client.SendAsync(Authorized(HttpMethod.Post, "/opportunities", admin,
            new Dictionary<string, object> { ["partyId"] = partyId, ["currency"] = "EUR", ["estimatedAmount"] = 10, ["customFields"] = new Dictionary<string, object> { [key] = "north" } }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("opportunityId").GetInt64();

        var detail = await DetailAsync(client, admin, id);
        Assert.Equal("north", detail.GetProperty("customFields").GetProperty(key).GetString());

        var updated = await client.SendAsync(Authorized(HttpMethod.Put, $"/opportunities/{id}/custom-fields", admin,
            new { expectedVersion = detail.GetProperty("rowVersion").GetInt64(), customFields = new Dictionary<string, object> { [key] = "south" } }));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("south", (await DetailAsync(client, admin, id)).GetProperty("customFields").GetProperty(key).GetString());

        var summaries = await (await client.SendAsync(Authorized(HttpMethod.Get, "/opportunities?take=200", admin))).Content.ReadFromJsonAsync<JsonElement>();
        var summary = summaries.EnumerateArray().Single(s => s.GetProperty("id").GetInt64() == id);
        Assert.Equal("south", summary.GetProperty("customFields").GetProperty(key).GetString());
    }

    [Fact]
    public async Task Invalid_values_are_a_422_with_per_field_errors()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var admin = await TokenAsync(client, DevSeeder.AdminEmail, 1);
        var key = UniqueKey("score");
        await DefineAsync(client, admin, new { key, label = "Score", type = "number", isRequired = false, sortOrder = 2, config = new { min = 0, max = 10 } });

        var partyId = await SeededPartyIdAsync(client, admin);
        var response = await client.SendAsync(Authorized(HttpMethod.Post, "/opportunities", admin,
            new Dictionary<string, object> { ["partyId"] = partyId, ["currency"] = "EUR", ["estimatedAmount"] = 10, ["customFields"] = new Dictionary<string, object> { [key] = 99, ["ghost_field"] = "x" } }));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("custom_field_invalid", problem.GetProperty("type").GetString());
        var errors = problem.GetProperty("errors").EnumerateArray().Select(e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString())).ToArray();
        Assert.Contains((key, "out_of_range"), errors);
        Assert.Contains(("ghost_field", "unknown_field"), errors);
    }

    [Fact]
    public async Task Deprecate_reports_impact_hides_nothing_and_reactivate_restores_it()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var admin = await TokenAsync(client, DevSeeder.AdminEmail, 1);
        var key = UniqueKey("legacy");
        var (definitionId, rowVersion) = await DefineAsync(client, admin, new { key, label = "Legacy", type = "text", isRequired = false, sortOrder = 3 });

        var partyId = await SeededPartyIdAsync(client, admin);
        var created = await client.SendAsync(Authorized(HttpMethod.Post, "/opportunities", admin,
            new Dictionary<string, object> { ["partyId"] = partyId, ["currency"] = "EUR", ["estimatedAmount"] = 10, ["customFields"] = new Dictionary<string, object> { [key] = "kept" } }));
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("opportunityId").GetInt64();

        var impact = await (await client.SendAsync(Authorized(HttpMethod.Get, $"/crm/settings/custom-fields/{definitionId}/impact", admin))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, impact.GetProperty("opportunitiesWithValue").GetInt32());

        var deprecated = await client.SendAsync(Authorized(HttpMethod.Post, $"/crm/settings/custom-fields/{definitionId}/deprecate", admin, new { expectedRowVersion = rowVersion }));
        Assert.Equal(HttpStatusCode.OK, deprecated.StatusCode);
        var deprecatedVersion = (await deprecated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rowVersion").GetInt64();

        // The stored value stays readable; writing it is refused.
        var detail = await DetailAsync(client, admin, id);
        Assert.Equal("kept", detail.GetProperty("customFields").GetProperty(key).GetString());
        var write = await client.SendAsync(Authorized(HttpMethod.Put, $"/opportunities/{id}/custom-fields", admin,
            new { expectedVersion = detail.GetProperty("rowVersion").GetInt64(), customFields = new Dictionary<string, object> { [key] = "changed" } }));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, write.StatusCode);

        var again = await client.SendAsync(Authorized(HttpMethod.Post, $"/crm/settings/custom-fields/{definitionId}/deprecate", admin, new { expectedRowVersion = deprecatedVersion }));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var reactivated = await client.SendAsync(Authorized(HttpMethod.Post, $"/crm/settings/custom-fields/{definitionId}/reactivate", admin, new { expectedRowVersion = deprecatedVersion }));
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
    }

    [Fact]
    public async Task A_duplicate_key_is_a_409_and_a_viewer_cannot_manage_or_write()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var admin = await TokenAsync(client, DevSeeder.AdminEmail, 1);
        var key = UniqueKey("dup");
        await DefineAsync(client, admin, new { key, label = "Dup", type = "text", isRequired = false, sortOrder = 4 });

        var duplicate = await client.SendAsync(Authorized(HttpMethod.Post, "/crm/settings/custom-fields", admin, new { key, label = "Dup 2", type = "text", isRequired = false, sortOrder = 5 }));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("custom_field_key_conflict", (await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString());

        var viewer = await TokenAsync(client, DevSeeder.ViewerEmail);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authorized(HttpMethod.Get, "/crm/settings/custom-fields", viewer))).StatusCode);
        var denied = await client.SendAsync(Authorized(HttpMethod.Post, "/crm/settings/custom-fields", viewer, new { key = UniqueKey("nope"), label = "Nope", type = "text", isRequired = false, sortOrder = 0 }));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var partyId = await SeededPartyIdAsync(client, admin);
        var created = await client.SendAsync(Authorized(HttpMethod.Post, "/opportunities", admin, new { partyId, currency = "EUR", estimatedAmount = 10 }));
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("opportunityId").GetInt64();
        var version = (await DetailAsync(client, admin, id)).GetProperty("rowVersion").GetInt64();
        var viewerWrite = await client.SendAsync(Authorized(HttpMethod.Put, $"/opportunities/{id}/custom-fields", viewer,
            new { expectedVersion = version, customFields = new Dictionary<string, object> { [key] = "x" } }));
        Assert.Contains(viewerWrite.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });
    }
}
