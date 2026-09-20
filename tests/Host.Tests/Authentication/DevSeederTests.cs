using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CRM.Application;
using Host.Authentication;
using Host.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace Host.Tests.Authentication;

/// <summary>The local-development seed: three identities that end in three different login
/// states, idempotent, and inert outside Development.</summary>
[Collection(HostIntegrationCollection.Name)]
public sealed class DevSeederTests : IClassFixture<AuthApiFixture>
{
    private const string SeedPassword = "Seed-Test-Passw0rd-1";

    private static readonly Dictionary<string, string?> SeedEnabled = new()
    {
        ["DevSeed__Enabled"] = "true",
        ["DevSeed__Password"] = SeedPassword,
    };

    private readonly AuthApiFixture _fixture;

    public DevSeederTests(AuthApiFixture fixture) => _fixture = fixture;

    private static async Task<JsonElement> LoginAsync(HttpClient client, string email, string password = SeedPassword)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login") { Content = JsonContent.Create(new { email, password }) };
        request.Headers.Add("X-Requested-With", "fynovio");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>());
    }

    private static async Task<string> SelectTenantAsync(HttpClient client, string loginCookie, long tenantId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/tenants/select") { Content = JsonContent.Create(new { tenantId }) };
        request.Headers.Add("X-Requested-With", "fynovio");
        request.Headers.Add("Cookie", loginCookie);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }

    /// <summary>Logs in and returns the body plus the `name=value` cookie pair for follow-up calls.</summary>
    private static async Task<(JsonElement Body, string Cookie)> LoginWithCookieAsync(HttpClient client, string email)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login") { Content = JsonContent.Create(new { email, password = SeedPassword }) };
        request.Headers.Add("X-Requested-With", "fynovio");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var setCookie = response.Headers.GetValues("Set-Cookie").First(v => v.StartsWith("fynovio_rt=", StringComparison.Ordinal));
        return (await response.Content.ReadFromJsonAsync<JsonElement>(), setCookie.Split(';')[0]);
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    private async Task<long> CredentialCountAsync(string email)
    {
        await using var connection = new NpgsqlConnection(_fixture.AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM identity.account_credentials WHERE login_email_normalized = @e", connection);
        command.Parameters.AddWithValue("e", email);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task Admin_signs_in_to_the_tenant_selection_state_and_can_use_either_tenant()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();

        var (body, cookie) = await LoginWithCookieAsync(client, DevSeeder.AdminEmail);

        Assert.Equal("tenant_selection_required", body.GetProperty("status").GetString());
        Assert.False(body.TryGetProperty("accessToken", out _));
        var tenants = body.GetProperty("memberships").EnumerateArray().Select(m => m.GetProperty("tenantId").GetInt64()).Order().ToArray();
        Assert.Equal([1L, 2L], tenants);

        foreach (var tenant in tenants)
        {
            var token = await SelectTenantAsync(client, cookie, tenant);
            Assert.Equal(HttpStatusCode.OK, (await GetAsync(client, "/opportunities", token)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await GetAsync(client, "/auth/me", token)).StatusCode);
        }
    }

    [Fact]
    public async Task Single_tenant_user_is_signed_in_directly_but_holds_no_crm_grants()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();

        var body = await LoginAsync(client, DevSeeder.SingleTenantEmail);

        Assert.Equal("authenticated", body.GetProperty("status").GetString());
        Assert.Equal(1, body.GetProperty("activeTenant").GetProperty("tenantId").GetInt64());

        var create = new HttpRequestMessage(HttpMethod.Post, "/opportunities")
        {
            Content = JsonContent.Create(new { partyId = 1, currency = "EUR", estimatedAmount = 100 })
        };
        create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        create.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(create)).StatusCode); // authenticated ≠ permitted
    }

    [Fact]
    public async Task Account_without_membership_ends_in_the_no_membership_state()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();

        var body = await LoginAsync(client, DevSeeder.NoMembershipEmail);

        Assert.Equal("no_membership", body.GetProperty("status").GetString());
        Assert.False(body.TryGetProperty("accessToken", out _));
    }

    [Fact]
    public async Task Wrong_password_is_still_rejected_for_seeded_accounts()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
        {
            Content = JsonContent.Create(new { email = DevSeeder.AdminEmail, password = "definitely-not-the-password" })
        };
        request.Headers.Add("X-Requested-With", "fynovio");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }

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

    private static async Task<string> AdminTokenAsync(HttpClient client, long tenantId)
    {
        var (_, cookie) = await LoginWithCookieAsync(client, DevSeeder.AdminEmail);
        return await SelectTenantAsync(client, cookie, tenantId);
    }

    [Fact]
    public async Task Admin_holds_the_crm_grants_and_the_seeded_pipeline_assigns_the_entry_stage_on_open()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();

        foreach (var tenantId in new long[] { 1, 2 })
        {
            var token = await AdminTokenAsync(client, tenantId);

            var created = await client.SendAsync(Authorized(HttpMethod.Post, "/opportunities", token, new { partyId = 1001, currency = "EUR", estimatedAmount = 250 }));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var opportunityId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("opportunityId").GetInt64();

            var opened = await client.SendAsync(Authorized(HttpMethod.Post, $"/opportunities/{opportunityId}/open", token,
                new { expectedVersion = 1, expiryDate = DateTimeOffset.UtcNow.AddDays(30) }));
            Assert.Equal(HttpStatusCode.OK, opened.StatusCode);

            var detail = await (await client.SendAsync(Authorized(HttpMethod.Get, $"/opportunities/{opportunityId}", token))).Content.ReadFromJsonAsync<JsonElement>();
            var versionId = detail.GetProperty("pipelineDefinitionVersionId").GetInt64();
            var entryStageId = detail.GetProperty("pipelineStageId").GetInt64();

            var stages = await (await client.SendAsync(Authorized(HttpMethod.Get, $"/pipelines/{versionId}/stages", token))).Content.ReadFromJsonAsync<JsonElement>();
            var byName = stages.EnumerateArray().ToDictionary(s => s.GetProperty("name").GetString()!);
            Assert.Equal(entryStageId, byName["Qualification"].GetProperty("id").GetInt64());
            Assert.True(byName["Qualification"].GetProperty("isEntry").GetBoolean());
            Assert.Equal(CrmDevSeed.ActiveStageNames.Count + 1, byName.Count);
            Assert.False(byName[CrmDevSeed.RetiredStageName].GetProperty("isActive").GetBoolean());
        }
    }

    [Fact]
    public async Task Viewer_reads_opportunities_but_every_mutation_is_denied_and_no_action_is_available()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();

        var adminToken = await AdminTokenAsync(client, 1);
        var created = await client.SendAsync(Authorized(HttpMethod.Post, "/opportunities", adminToken, new { partyId = 1002, currency = "TRY", estimatedAmount = 10 }));
        var opportunityId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("opportunityId").GetInt64();

        var viewer = await LoginAsync(client, DevSeeder.ViewerEmail);
        var viewerToken = viewer.GetProperty("accessToken").GetString()!;

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authorized(HttpMethod.Get, "/opportunities", viewerToken))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Authorized(HttpMethod.Get, $"/opportunities/{opportunityId}", viewerToken))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Authorized(HttpMethod.Post, "/opportunities", viewerToken, new { partyId = 1, currency = "EUR", estimatedAmount = 1 }))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Authorized(HttpMethod.Post, $"/opportunities/{opportunityId}/lose", viewerToken, new { expectedVersion = 1, lostReason = "n/a" }))).StatusCode);

        var actions = await (await client.SendAsync(Authorized(HttpMethod.Get, $"/opportunities/{opportunityId}/actions", viewerToken))).Content.ReadFromJsonAsync<JsonElement>();
        foreach (var flag in new[] { "canOpen", "canChangeStage", "canWin", "canLose", "canReassign" })
            Assert.False(actions.GetProperty(flag).GetBoolean(), flag);
        Assert.Empty(actions.GetProperty("allowedTargetStageIds").EnumerateArray());
    }

    [Fact]
    public async Task Sales_rep_works_opportunities_but_cannot_reassign()
    {
        using var host = await _fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();

        var rep = await LoginAsync(client, DevSeeder.SalesRepEmail);
        var repToken = rep.GetProperty("accessToken").GetString()!;
        var created = await client.SendAsync(Authorized(HttpMethod.Post, "/opportunities", repToken, new { partyId = 1003, currency = "EUR", estimatedAmount = 5 }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var opportunityId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("opportunityId").GetInt64();

        var actions = await (await client.SendAsync(Authorized(HttpMethod.Get, $"/opportunities/{opportunityId}/actions", repToken))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(actions.GetProperty("canOpen").GetBoolean());
        Assert.True(actions.GetProperty("canLose").GetBoolean());
        Assert.False(actions.GetProperty("canReassign").GetBoolean());
    }

    private async Task<long> CountAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_fixture.AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task Seeding_twice_is_idempotent()
    {
        using (await _fixture.StartHostAsync(SeedEnabled)) { }
        using var second = await _fixture.StartHostAsync(SeedEnabled); // re-runs the seed against the seeded database
        using var client = second.CreateClient();

        Assert.Equal(1, await CredentialCountAsync(DevSeeder.AdminEmail));
        Assert.Equal(1, await CredentialCountAsync(DevSeeder.SingleTenantEmail));
        Assert.Equal(1, await CredentialCountAsync(DevSeeder.NoMembershipEmail));
        Assert.Equal(1, await CredentialCountAsync(DevSeeder.ViewerEmail));
        Assert.Equal(2, await CountAsync("SELECT count(*) FROM crm.pipeline_definitions"));
        Assert.Equal(1, await CredentialCountAsync(DevSeeder.SalesRepEmail));
        // The CRM roles come from the production enablement path: one template copy per tenant, one assignment per
        // administrator, and re-seeding neither duplicates a copy nor re-grants.
        Assert.Equal(2, await CountAsync("SELECT count(*) FROM access.tenant_module_enablements WHERE module_key = 'crm'"));
        foreach (var roleKey in new[] { CrmModuleCapabilities.ManagerRoleKey, CrmModuleCapabilities.ViewerRoleKey, CrmModuleCapabilities.SalesRepresentativeRoleKey })
            Assert.Equal(2, await CountAsync($"SELECT count(*) FROM access.roles WHERE key = '{roleKey}' AND origin = 'system_template' AND origin_module_key = 'crm'"));
        Assert.Equal(2, await CountAsync("SELECT count(*) FROM access.role_assignments WHERE source = 'module_enablement'"));
        Assert.Equal(2, await CountAsync("SELECT count(*) FROM access.role_assignments WHERE source = 'manual' AND reason = 'Development seed'"));
        Assert.Equal("tenant_selection_required", (await LoginAsync(client, DevSeeder.AdminEmail)).GetProperty("status").GetString());
    }
}

/// <summary>Own fixture (own database): the guard must leave a pristine database untouched.</summary>
[Collection(HostIntegrationCollection.Name)]
public sealed class DevSeederGuardTests : IClassFixture<AuthApiFixture>
{
    private readonly AuthApiFixture _fixture;

    public DevSeederGuardTests(AuthApiFixture fixture) => _fixture = fixture;

    private async Task<long> AccountCountAsync()
    {
        await using var connection = new NpgsqlConnection(_fixture.AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM identity.account_credentials", connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task Enabled_flag_alone_never_seeds_outside_development()
    {
        var production = new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["DevSeed__Enabled"] = "true",
            ["DevSeed__Password"] = "Should-Never-Be-Used-1",
            ["Authentication__Jwt__Issuer"] = "https://prod.example",
            ["Authentication__Jwt__Audience"] = "fynovio-platform",
            ["Authentication__Jwt__SigningKey"] = new string('k', 48),
            ["Authentication__PublicAppBaseUrl"] = "https://app.prod.example",
        };

        using var host = await _fixture.StartHostAsync(production);

        Assert.Equal(0, await AccountCountAsync());
    }

    [Fact]
    public async Task Enabling_the_seed_without_a_password_fails_fast_in_development()
    {
        var noPassword = new Dictionary<string, string?> { ["DevSeed__Enabled"] = "true", ["DevSeed__Password"] = "" };

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            using var host = await _fixture.StartHostAsync(noPassword);
        });
        Assert.Equal(0, await AccountCountAsync());
    }
}
