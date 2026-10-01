using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Host.Authentication;
using Host.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
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
            key,
            label = "Region",
            type = "select",
            isRequired = false,
            sortOrder = 1,
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
    public async Task The_list_filters_on_an_option_value_and_refuses_an_unfilterable_or_malformed_filter()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var admin = await TokenAsync(client, DevSeeder.AdminEmail, 1);
        var key = UniqueKey("tier");
        var note = UniqueKey("note");
        await DefineAsync(client, admin, new
        {
            key,
            label = "Tier",
            type = "select",
            isRequired = false,
            sortOrder = 6,
            config = new { options = new[] { new { key = "gold", label = "Gold" }, new { key = "silver", label = "Silver" } } }
        });
        await DefineAsync(client, admin, new { key = note, label = "Note", type = "text", isRequired = false, sortOrder = 7 });

        var partyId = await SeededPartyIdAsync(client, admin);
        async Task<long> CreateAsync(string tier)
        {
            var created = await client.SendAsync(Authorized(HttpMethod.Post, "/opportunities", admin,
                new Dictionary<string, object> { ["partyId"] = partyId, ["currency"] = "EUR", ["estimatedAmount"] = 10, ["customFields"] = new Dictionary<string, object> { [key] = tier } }));
            return (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("opportunityId").GetInt64();
        }

        var gold = await CreateAsync("gold");
        await CreateAsync("silver");

        var filtered = await client.SendAsync(Authorized(HttpMethod.Get, $"/opportunities?take=200&cf={key}:gold", admin));
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        Assert.Equal([gold], (await filtered.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Select(s => s.GetProperty("id").GetInt64()).ToArray());

        var unfilterable = await client.SendAsync(Authorized(HttpMethod.Get, $"/opportunities?cf={note}:x", admin));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unfilterable.StatusCode);
        Assert.Equal("not_filterable", (await unfilterable.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codes").GetProperty(note)[0].GetString());

        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(Authorized(HttpMethod.Get, $"/opportunities?cf={key}", admin))).StatusCode);
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
        var codes = problem.GetProperty("codes");
        Assert.Equal("out_of_range", codes.GetProperty(key)[0].GetString());
        Assert.Equal("unknown_field", codes.GetProperty("ghost_field")[0].GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("errors").GetProperty(key)[0].GetString()));
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

    /// <summary>Definitions moved to the Semantic Catalog; the error contract of the field endpoints must not have moved with
    /// them (adr-semantic-catalog-changeset.md S-2): same statuses, same `type` codes.</summary>
    [Fact]
    public async Task The_catalog_keeps_the_error_contract_of_the_field_endpoints()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var admin = await TokenAsync(client, DevSeeder.AdminEmail, 1);

        async Task<(HttpStatusCode Status, string? Type)> SendAsync(HttpMethod method, string path, object body, string? idempotencyKey = null)
        {
            var response = await client.SendAsync(Authorized(method, path, admin, body, idempotencyKey));
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            return (response.StatusCode, json.TryGetProperty("type", out var type) ? type.GetString() : null);
        }

        var key = UniqueKey("err");
        var (id, version) = await DefineAsync(client, admin, new { key, label = "Err", type = "text", isRequired = false, sortOrder = 1 });

        Assert.Equal((HttpStatusCode.Conflict, "concurrency_conflict"),
            await SendAsync(HttpMethod.Put, $"/crm/settings/custom-fields/{id}", new { label = "Err", isRequired = false, sortOrder = 1, expectedRowVersion = version + 7 }));
        Assert.Equal((HttpStatusCode.NotFound, "not_found"),
            await SendAsync(HttpMethod.Post, "/crm/settings/custom-fields/999999999/deprecate", new { expectedRowVersion = 1 }));
        Assert.Equal((HttpStatusCode.BadRequest, "validation_error"),
            await SendAsync(HttpMethod.Post, "/crm/settings/custom-fields", new { key = UniqueKey("bad"), label = "Bad", type = "select", isRequired = false, sortOrder = 0 }));
        Assert.Equal((HttpStatusCode.BadRequest, "validation_error"),
            await SendAsync(HttpMethod.Post, "/crm/settings/custom-fields", new { key = UniqueKey("odd"), label = "Odd", type = "money", isRequired = false, sortOrder = 0 }));

        var reused = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.Created,
            (await SendAsync(HttpMethod.Post, "/crm/settings/custom-fields", new { key = UniqueKey("idem"), label = "Idem", type = "text", isRequired = false, sortOrder = 0 }, reused)).Status);
        Assert.Equal((HttpStatusCode.Conflict, "idempotency_key_reused"),
            await SendAsync(HttpMethod.Post, "/crm/settings/custom-fields", new { key = UniqueKey("idem"), label = "Different", type = "text", isRequired = false, sortOrder = 0 }, reused));
    }

    [Fact]
    public async Task Every_allow_listed_reference_target_has_a_registered_link_resolver()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var scope = host.Services.CreateScope();
        var resolvers = scope.ServiceProvider.GetServices<Contracts.ILinkTargetResolver>().Select(r => (r.BoundedContext, r.EntityType)).ToHashSet();

        // An allow-listed target with no resolver would make every write of that reference a silent 422.
        Assert.All(SemanticCatalog.Domain.ReferenceTargets.Allowed, target => Assert.Contains((target.BoundedContext, target.EntityType), resolvers));
    }

    [Fact]
    public async Task A_reference_field_is_defined_verified_on_write_and_hydrated_per_reader_through_the_api()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var admin = await TokenAsync(client, DevSeeder.AdminEmail, 1);
        var key = UniqueKey("account");
        await DefineAsync(client, admin, new { key, label = "Account", type = "reference", isRequired = false, sortOrder = 1, config = new { target = new { boundedContext = "masterdata", entityType = "party" } } });

        var listed = (await (await client.SendAsync(Authorized(HttpMethod.Get, "/crm/settings/custom-fields", admin))).Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray().Single(d => d.GetProperty("fieldName").GetString() == key);
        Assert.Equal("reference", listed.GetProperty("fieldType").GetString());
        Assert.Equal("party", listed.GetProperty("config").GetProperty("target").GetProperty("entityType").GetString());

        var partyId = await SeededPartyIdAsync(client, admin);
        var created = await client.SendAsync(Authorized(HttpMethod.Post, "/opportunities", admin,
            new Dictionary<string, object> { ["partyId"] = partyId, ["currency"] = "EUR", ["estimatedAmount"] = 10, ["customFields"] = new Dictionary<string, object> { [key] = partyId } }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("opportunityId").GetInt64();

        var detail = await DetailAsync(client, admin, id);
        Assert.Equal(partyId, detail.GetProperty("customFields").GetProperty(key).GetInt64());
        var hydrated = detail.GetProperty("customFieldReferences").GetProperty(key);
        Assert.True(hydrated.GetProperty("accessible").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(hydrated.GetProperty("label").GetString()));

        var summaries = await (await client.SendAsync(Authorized(HttpMethod.Get, "/opportunities?take=200", admin))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(summaries.EnumerateArray().Single(s => s.GetProperty("id").GetInt64() == id).GetProperty("customFieldReferences").GetProperty(key).GetProperty("accessible").GetBoolean());

        // An id that is not a visible party is a 422 with the reference code, and says nothing about why.
        var refused = await client.SendAsync(Authorized(HttpMethod.Put, $"/opportunities/{id}/custom-fields", admin,
            new { expectedVersion = detail.GetProperty("rowVersion").GetInt64(), customFields = new Dictionary<string, object> { [key] = 987654321 } }));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("custom_field_invalid", problem.GetProperty("type").GetString());
        Assert.Equal("invalid_reference", problem.GetProperty("codes").GetProperty(key)[0].GetString());

        // A reader without the party-search action sees the id but no label (the resolver's authorization is the reader's own).
        var viewer = await TokenAsync(client, DevSeeder.ViewerEmail);
        var viewerDetail = await client.SendAsync(Authorized(HttpMethod.Get, $"/opportunities/{id}", viewer));
        Assert.Equal(HttpStatusCode.OK, viewerDetail.StatusCode);
        var asViewer = (await viewerDetail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("customFieldReferences").GetProperty(key);
        Assert.Equal(partyId, asViewer.GetProperty("id").GetInt64());
        Assert.False(asViewer.GetProperty("accessible").GetBoolean());
        Assert.Equal(JsonValueKind.Null, asViewer.GetProperty("label").ValueKind);
    }

    [Fact]
    public async Task Shared_views_are_managed_as_change_sets_read_by_every_crm_reader_and_shown_in_the_impact_of_a_field()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var admin = await TokenAsync(client, DevSeeder.AdminEmail, 1);
        var field = UniqueKey("vf");
        var (fieldId, fieldVersion) = await DefineAsync(client, admin, new { key = field, label = "View field", type = "text", isRequired = false, sortOrder = 1 });
        var viewKey = UniqueKey("view");

        var created = await client.SendAsync(Authorized(HttpMethod.Post, "/crm/settings/views", admin, new
        {
            key = viewKey,
            name = "Pipeline review",
            sortOrder = 1,
            columns = new object[] { new { kind = "builtin", key = "id" }, new { kind = "field", key = field }, new { kind = "builtin", key = "status" } },
        }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var viewId = createdBody.GetProperty("definitionId").GetInt64();
        Assert.True(createdBody.GetProperty("changeSetId").GetInt64() > 0);

        // Every CRM reader (the viewer) can read views; only a settings manager can change them.
        var viewer = await TokenAsync(client, DevSeeder.ViewerEmail);
        var listed = (await (await client.SendAsync(Authorized(HttpMethod.Get, "/crm/settings/views", viewer))).Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray().Single(v => v.GetProperty("key").GetString() == viewKey);
        Assert.Equal(["id", field, "status"], listed.GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("key").GetString()));
        Assert.Equal("Active", listed.GetProperty("status").GetString());
        var denied = await client.SendAsync(Authorized(HttpMethod.Post, "/crm/settings/views", viewer, new { key = UniqueKey("nope"), name = "Nope", columns = new[] { new { kind = "builtin", key = "id" } } }));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        // The deprecate dialog's first question: what depends on this field?
        var impact = await (await client.SendAsync(Authorized(HttpMethod.Get, $"/crm/settings/custom-fields/{fieldId}/impact", admin))).Content.ReadFromJsonAsync<JsonElement>();
        var dependent = Assert.Single(impact.GetProperty("dependentViews").EnumerateArray());
        Assert.Equal(viewKey, dependent.GetProperty("key").GetString());

        // Deprecating the field is still allowed; the view keeps its (now retired) column.
        var deprecated = await client.SendAsync(Authorized(HttpMethod.Post, $"/crm/settings/custom-fields/{fieldId}/deprecate", admin, new { expectedRowVersion = fieldVersion }));
        Assert.Equal(HttpStatusCode.OK, deprecated.StatusCode);

        // Edit and retire the view; the error contract is the catalog's.
        var updated = await client.SendAsync(Authorized(HttpMethod.Put, $"/crm/settings/views/{viewId}", admin, new
        {
            name = "Pipeline review v2",
            sortOrder = 2,
            expectedRowVersion = 1,
            columns = new[] { new { kind = "builtin", key = "id" } },
        }));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(2, (await updated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rowVersion").GetInt64());

        async Task<(HttpStatusCode Status, string? Type)> SendAsync(HttpMethod method, string path, object body)
        {
            var response = await client.SendAsync(Authorized(method, path, admin, body));
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            return (response.StatusCode, json.TryGetProperty("type", out var type) ? type.GetString() : null);
        }

        Assert.Equal((HttpStatusCode.Conflict, "view_key_conflict"),
            await SendAsync(HttpMethod.Post, "/crm/settings/views", new { key = viewKey, name = "Again", columns = new[] { new { kind = "builtin", key = "id" } } }));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "view_column_unknown"),
            await SendAsync(HttpMethod.Post, "/crm/settings/views", new { key = UniqueKey("ghost"), name = "Ghost", columns = new[] { new { kind = "field", key = "no_such_field" } } }));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "view_column_deprecated"),
            await SendAsync(HttpMethod.Post, "/crm/settings/views", new { key = UniqueKey("old"), name = "Old", columns = new[] { new { kind = "field", key = field } } }));
        Assert.Equal((HttpStatusCode.BadRequest, "validation_error"),
            await SendAsync(HttpMethod.Post, "/crm/settings/views", new { key = UniqueKey("bad"), name = "Bad", columns = new[] { new { kind = "builtin", key = "serviceDuration" } } }));
        Assert.Equal((HttpStatusCode.Conflict, "concurrency_conflict"),
            await SendAsync(HttpMethod.Put, $"/crm/settings/views/{viewId}", new { name = "Stale", columns = new[] { new { kind = "builtin", key = "id" } }, expectedRowVersion = 1 }));
        Assert.Equal((HttpStatusCode.NotFound, "not_found"), await SendAsync(HttpMethod.Post, "/crm/settings/views/999999999/deprecate", new { expectedRowVersion = 1 }));

        var retired = await client.SendAsync(Authorized(HttpMethod.Post, $"/crm/settings/views/{viewId}/deprecate", admin, new { expectedRowVersion = 2 }));
        Assert.Equal(HttpStatusCode.OK, retired.StatusCode);
        var afterwards = (await (await client.SendAsync(Authorized(HttpMethod.Get, "/crm/settings/views", admin))).Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray().Single(v => v.GetProperty("key").GetString() == viewKey);
        Assert.Equal("Deprecated", afterwards.GetProperty("status").GetString());
    }
}
