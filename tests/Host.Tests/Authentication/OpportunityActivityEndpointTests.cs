using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Host.Authentication;
using Host.Tests.Fixtures;
using Xunit;

namespace Host.Tests.Authentication;

/// <summary>The activity timeline through the real stack (seeded identities, real PDP, RLS as the runtime role). The
/// projection is filled by the Worker's consumer, not by Host, so here it is empty; what is proven is that the endpoint
/// answers exactly like the opportunity detail for every caller (adr-event-consumption.md, E-5).</summary>
[Collection(HostIntegrationCollection.Name)]
public sealed class OpportunityActivityEndpointTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    private const string SeedPassword = "Seed-Test-Passw0rd-1";

    private static readonly Dictionary<string, string?> SeedEnabled = new()
    {
        ["DevSeed__Enabled"] = "true",
        ["DevSeed__Password"] = SeedPassword,
    };

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

    [Fact]
    public async Task The_timeline_answers_like_the_opportunity_detail_for_every_caller()
    {
        using var host = await fixture.StartHostAsync(SeedEnabled);
        using var client = host.CreateClient();
        var admin = await TokenAsync(client, DevSeeder.AdminEmail, 1);
        var parties = await (await client.SendAsync(Authorized(HttpMethod.Get, "/crm/references/parties?take=1", admin))).Content.ReadFromJsonAsync<JsonElement>();
        var created = await client.SendAsync(Authorized(HttpMethod.Post, "/opportunities", admin,
            new { partyId = parties[0].GetProperty("id").GetInt64(), currency = "EUR", estimatedAmount = 10 }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("opportunityId").GetInt64();

        var adminTimeline = await client.SendAsync(Authorized(HttpMethod.Get, $"/opportunities/{id}/activity", admin));
        Assert.Equal(HttpStatusCode.OK, adminTimeline.StatusCode);
        Assert.Equal(JsonValueKind.Array, (await adminTimeline.Content.ReadFromJsonAsync<JsonElement>()).ValueKind);

        foreach (var email in new[] { DevSeeder.ViewerEmail, DevSeeder.SalesRepEmail, DevSeeder.SingleTenantEmail })
        {
            var token = await TokenAsync(client, email);
            var detail = await client.SendAsync(Authorized(HttpMethod.Get, $"/opportunities/{id}", token));
            var timeline = await client.SendAsync(Authorized(HttpMethod.Get, $"/opportunities/{id}/activity", token));
            Assert.Equal(detail.StatusCode, timeline.StatusCode);
        }

        var missing = await client.SendAsync(Authorized(HttpMethod.Get, "/opportunities/999999999/activity", admin));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
