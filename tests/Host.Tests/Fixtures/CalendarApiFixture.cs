using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Host.Authentication;
using Xunit;

namespace Host.Tests.Fixtures;

/// <summary>One database and one real host (seeded identities, real PDP, RLS as the runtime role) shared by every calendar
/// HTTP test, with the four identities the calendar contract needs already logged in: two accounts in tenant 1 that both hold
/// the collaboration role (the administrator and the sales representative), the administrator again in tenant 2, and an
/// account in tenant 1 without any grant. Access tokens live 10 minutes, far longer than this class runs.</summary>
public sealed class CalendarApiFixture : IAsyncLifetime
{
    private const string SeedPassword = "Seed-Test-Passw0rd-1";

    private readonly AuthApiFixture _database = new();
    private AuthApiHost? _host;

    internal CapturingLoggerProvider Logs { get; } = new();

    internal HttpClient Client { get; private set; } = null!;

    /// <summary>Administrator, tenant 1: owns the entries most tests create.</summary>
    public string AdminTenantOne { get; private set; } = "";

    /// <summary>The same administrator account, tenant 2: same principal, different tenant.</summary>
    public string AdminTenantTwo { get; private set; } = "";

    /// <summary>Sales representative, tenant 1: a different owner in the same tenant, also holds the collaboration role.</summary>
    public string RepTenantOne { get; private set; } = "";

    /// <summary>Member of tenant 1 without any grant: every calendar capability check is denied.</summary>
    public string NoRoleTenantOne { get; private set; } = "";

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _host = await _database.StartHostAsync(
            new Dictionary<string, string?> { ["DevSeed__Enabled"] = "true", ["DevSeed__Password"] = SeedPassword },
            logProvider: Logs);
        Client = _host.CreateClient();

        AdminTenantOne = await TokenAsync(DevSeeder.AdminEmail, DevSeeder.TenantOne.Value);
        AdminTenantTwo = await TokenAsync(DevSeeder.AdminEmail, DevSeeder.TenantTwo.Value);
        RepTenantOne = await TokenAsync(DevSeeder.SalesRepEmail, tenantId: null);
        NoRoleTenantOne = await TokenAsync(DevSeeder.SingleTenantEmail, tenantId: null);
    }

    /// <summary>A fresh member of tenant 1 holding ONLY the given actions (each tenant-wide), logged in. Used to give a caller
    /// the calendar capability without CRM access, or with exactly one CRM grant that a test can then take away.</summary>
    internal async Task<CalendarMember> SeedMemberAsync(params string[] actionKeys)
    {
        var user = await _database.SeedUserAsync(tenantIds: DevSeeder.TenantOne.Value);
        var roles = new Dictionary<string, long>();
        foreach (var actionKey in actionKeys)
            roles[actionKey] = await _database.GrantAsync(user.AccountId, DevSeeder.TenantOne.Value, actionKey);

        return new CalendarMember(await TokenAsync(user.Email, tenantId: null, AuthApiFixture.Password), user.AccountId, roles);
    }

    internal Task RevokeAsync(CalendarMember member, string actionKey) =>
        _database.RevokeAsync(member.AccountId, DevSeeder.TenantOne.Value, member.Roles[actionKey]);

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        _host?.Dispose();
        await _database.DisposeAsync();
    }

    private async Task<string> TokenAsync(string email, long? tenantId, string password = SeedPassword)
    {
        var login = new HttpRequestMessage(HttpMethod.Post, "/auth/login") { Content = JsonContent.Create(new { email, password }) };
        login.Headers.Add("X-Requested-With", "fynovio");
        var loginResponse = await Client.SendAsync(login);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        if (tenantId is null)
            return body.GetProperty("accessToken").GetString()!;

        var cookie = loginResponse.Headers.GetValues("Set-Cookie").First(v => v.StartsWith("fynovio_rt=", StringComparison.Ordinal)).Split(';')[0];
        var select = new HttpRequestMessage(HttpMethod.Post, "/auth/tenants/select") { Content = JsonContent.Create(new { tenantId }) };
        select.Headers.Add("X-Requested-With", "fynovio");
        select.Headers.Add("Cookie", cookie);
        var selectResponse = await Client.SendAsync(select);
        Assert.Equal(HttpStatusCode.OK, selectResponse.StatusCode);
        return (await selectResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }
}

/// <summary>A member seeded by a test: their access token, and the role each granted action came from (so it can be revoked).</summary>
internal sealed record CalendarMember(string Token, long AccountId, IReadOnlyDictionary<string, long> Roles);
