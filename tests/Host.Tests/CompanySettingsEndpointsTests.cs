using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Host.Tests.Fixtures;
using Xunit;

namespace Host.Tests;

[Collection(HostIntegrationCollection.Name)]
public sealed class CompanySettingsEndpointsTests(CalendarApiFixture api) : IClassFixture<CalendarApiFixture>
{
    [Fact]
    public async Task Administrator_can_get_update_and_replay_company_settings()
    {
        var before = await GetAsync(api.AdminTenantOne);
        var body = new
        {
            displayName = "Updated development company",
            legalName = (string?)null,
            taxNumber = (string?)null,
            taxOffice = (string?)null,
            email = "settings@example.test",
            phone = (string?)null,
            address = (string?)null,
            timezone = "Europe/Istanbul",
            currencyCode = "TRY",
            expectedVersion = before.GetProperty("rowVersion").GetInt64()
        };

        var first = await PutAsync(api.AdminTenantOne, body, "company-settings-replay");
        var replay = await PutAsync(api.AdminTenantOne, body, "company-settings-replay");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var firstJson = await first.Content.ReadFromJsonAsync<JsonElement>();
        var replayJson = await replay.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(firstJson.GetProperty("replayed").GetBoolean());
        Assert.True(replayJson.GetProperty("replayed").GetBoolean());
        Assert.Equal("Updated development company", firstJson.GetProperty("settings").GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Caller_without_the_capability_is_forbidden()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/company/settings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.NoRoleTenantOne);

        var response = await api.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Administrator_can_list_and_cancel_a_pending_invitation()
    {
        var email = $"company-{Guid.NewGuid():N}@example.test";
        var invite = new HttpRequestMessage(HttpMethod.Post, "/company/settings/invitations")
        {
            Content = JsonContent.Create(new
            {
                email,
                displayName = "Pending Member",
                locale = "en",
                roleKey = "tenant_administrator"
            })
        };
        invite.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AdminTenantOne);
        invite.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var invited = await api.Client.SendAsync(invite);
        Assert.Equal(HttpStatusCode.Accepted, invited.StatusCode);

        var access = new HttpRequestMessage(HttpMethod.Get, "/company/settings/access");
        access.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AdminTenantOne);
        var overview = await api.Client.SendAsync(access);
        Assert.Equal(HttpStatusCode.OK, overview.StatusCode);
        var body = await overview.Content.ReadFromJsonAsync<JsonElement>();
        var pending = body.GetProperty("pendingInvitations").EnumerateArray()
            .Single(item => item.GetProperty("email").GetString() == email);
        Assert.Equal("tenant_administrator", pending.GetProperty("roleKey").GetString());

        var cancel = new HttpRequestMessage(HttpMethod.Delete,
            $"/company/settings/invitations/{pending.GetProperty("invitationId").GetGuid():D}");
        cancel.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.AdminTenantOne);
        cancel.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.NoContent, (await api.Client.SendAsync(cancel)).StatusCode);
    }

    private async Task<JsonElement> GetAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/company/settings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await api.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<HttpResponseMessage> PutAsync(string token, object body, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/company/settings") { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Idempotency-Key", key);
        return await api.Client.SendAsync(request);
    }
}
