using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Access.Persistence;
using Collaboration.Application;
using Contracts;
using CRM.Persistence;
using Host.Bootstrap;
using Host.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Host.Tests.Authentication.LifecycleTestSupport;

namespace Host.Tests.Authentication;

/// <summary>Phase 2.6 OD2 — the production path that gives a real tenant CRM access: an operator command that
/// copies the CRM capability template. Before it runs, a tenant administrator's CRM calls are denied; after it,
/// the very same administrator can work opportunities. No seed, no default credential.</summary>
[Collection(HostIntegrationCollection.Name)]
public sealed class EnableModuleCommandTests : IClassFixture<AuthApiFixture>
{
    private static readonly Dictionary<string, string?> Enabled = new() { ["Bootstrap__Enabled"] = "true" };

    private readonly AuthApiFixture _fixture;

    public EnableModuleCommandTests(AuthApiFixture fixture) => _fixture = fixture;

    private static async Task<(int Code, string Output, string Error)> RunEnableAsync(AuthApiHost host, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await EnableModuleCommand.RunAsync(
            host.Services, host.Services.GetRequiredService<IConfiguration>(), [EnableModuleCommand.Name, .. args], output, error);
        return (code, output.ToString(), error.ToString());
    }

    private static async Task<(int Code, string Output, string Error)> RunBootstrapAsync(AuthApiHost host, long tenant, string email, params string[] extra)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await BootstrapCommand.RunAsync(
            host.Services, host.Services.GetRequiredService<IConfiguration>(),
            [BootstrapCommand.Name, "--tenant-id", tenant.ToString(), "--email", email, "--display-name", "Ops Admin", .. extra], output, error);
        return (code, output.ToString(), error.ToString());
    }

    private static async Task<string> SignInAsync(HttpClient client, string bootstrapOutput, string email)
    {
        var token = Uri.UnescapeDataString(Regex.Match(bootstrapOutput, "#token=(\\S+)").Groups[1].Value);
        Assert.Equal(HttpStatusCode.NoContent, (await Reset(client, token, NewPassword)).StatusCode);
        var login = await Login(client, email, NewPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await Json(login)).GetProperty("accessToken").GetString()!;
    }

    private static async Task<long> SeedPartyAsync(AuthApiHost host, long tenant)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var created = await scope.ServiceProvider.GetRequiredService<MasterData.Application.CreatePartyHandler>().HandleAsync(
            new MasterData.Application.CreatePartyCommand(new TenantId(tenant), PartyType.Organization, "Acme", null, null, null, Guid.NewGuid().ToString(), Guid.NewGuid()));
        return created.PartyId;
    }

    private static async Task<HttpStatusCode> CreateOpportunityAsync(HttpClient client, string bearer, long partyId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/opportunities")
        {
            Content = JsonContent.Create(new { PartyId = partyId, Currency = "TRY", EstimatedAmount = 100m })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return (await client.SendAsync(request)).StatusCode;
    }

    [Fact]
    public void The_command_is_recognised_only_by_its_exact_name()
    {
        Assert.True(EnableModuleCommand.IsRequested([EnableModuleCommand.Name]));
        Assert.False(EnableModuleCommand.IsRequested([]));
        Assert.False(EnableModuleCommand.IsRequested(["enable"]));
        Assert.False(EnableModuleCommand.IsRequested(["--urls", EnableModuleCommand.Name]));
    }

    [Fact]
    public async Task It_refuses_to_run_unless_bootstrap_is_explicitly_enabled()
    {
        using var host = await _fixture.StartHostAsync();

        var (code, output, error) = await RunEnableAsync(host, "--tenant-id", "5", "--module", "crm");

        Assert.Equal(EnableModuleCommand.NotPermitted, code);
        Assert.Empty(output);
        Assert.Contains("Bootstrap:Enabled", error);
    }

    [Theory]
    [InlineData("--module", "crm")]
    [InlineData("--tenant-id", "0", "--module", "crm")]
    [InlineData("--tenant-id", "abc", "--module", "crm")]
    [InlineData("--tenant-id", "5")]
    public async Task Missing_or_malformed_arguments_print_usage(params string[] args)
    {
        using var host = await _fixture.StartHostAsync(Enabled);

        var (code, output, error) = await RunEnableAsync(host, args);

        Assert.Equal(EnableModuleCommand.BadArguments, code);
        Assert.Empty(output);
        Assert.Contains("Usage", error);
    }

    [Fact]
    public async Task An_unknown_module_is_refused()
    {
        var tenant = AuthApiFixture.NewTenantId();
        using var host = await _fixture.StartHostAsync(Enabled);
        Assert.Equal(BootstrapCommand.Success, (await RunBootstrapAsync(host, tenant, AuthApiFixture.NewEmail())).Code);

        var (code, output, error) = await RunEnableAsync(host, "--tenant-id", tenant.ToString(), "--module", "warehouse");

        Assert.Equal(EnableModuleCommand.BadArguments, code);
        Assert.Empty(output);
        Assert.Contains("Unknown module 'warehouse'", error);
    }

    [Fact]
    public async Task A_tenant_that_was_never_bootstrapped_is_refused()
    {
        using var host = await _fixture.StartHostAsync(Enabled);

        var (code, _, error) = await RunEnableAsync(host, "--tenant-id", AuthApiFixture.NewTenantId().ToString(), "--module", "crm");

        Assert.Equal(EnableModuleCommand.TenantNotBootstrapped, code);
        Assert.Contains("bootstrap-tenant-admin", error);
    }

    /// <summary>The whole point of OD2: a real tenant administrator — bootstrapped the production way — cannot use CRM
    /// until the module is enabled, and can immediately afterwards. Same token, no re-login: the PDP reads the grants live.</summary>
    [Fact]
    public async Task Enabling_crm_is_what_lets_a_bootstrapped_tenant_administrator_work_opportunities()
    {
        var tenant = AuthApiFixture.NewTenantId();
        var email = AuthApiFixture.NewEmail();
        using var host = await _fixture.StartHostAsync(Enabled);
        using var client = host.CreateClient();
        var bootstrap = await RunBootstrapAsync(host, tenant, email);
        Assert.Equal(BootstrapCommand.Success, bootstrap.Code);
        var bearer = await SignInAsync(client, bootstrap.Output, email);

        var partyId = await SeedPartyAsync(host, tenant);
        Assert.Equal(HttpStatusCode.Forbidden, await CreateOpportunityAsync(client, bearer, partyId));

        var (code, output, error) = await RunEnableAsync(host, "--tenant-id", tenant.ToString(), "--module", "crm");

        Assert.Equal(EnableModuleCommand.Success, code);
        Assert.Empty(error);
        Assert.Contains("enabled for tenant", output);
        Assert.Contains("1 administrator role assignment", output);
        Assert.Equal(HttpStatusCode.Created, await CreateOpportunityAsync(client, bearer, partyId));
    }

    [Fact]
    public async Task Running_it_again_is_a_no_op_that_still_succeeds()
    {
        var tenant = AuthApiFixture.NewTenantId();
        using var host = await _fixture.StartHostAsync(Enabled);
        Assert.Equal(BootstrapCommand.Success, (await RunBootstrapAsync(host, tenant, AuthApiFixture.NewEmail())).Code);
        Assert.Equal(EnableModuleCommand.Success, (await RunEnableAsync(host, "--tenant-id", tenant.ToString(), "--module", "crm")).Code);

        var (code, output, _) = await RunEnableAsync(host, "--tenant-id", tenant.ToString(), "--module=crm");

        Assert.Equal(EnableModuleCommand.Success, code);
        Assert.Contains("already enabled", output);
        await using var scope = host.Services.CreateAsyncScope();
        var access = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        Assert.Equal(1, await CountEnablementsAsync(access, tenant));
    }

    [Fact]
    public async Task Enabling_crm_provisions_a_default_pipeline()
    {
        var tenant = AuthApiFixture.NewTenantId();
        var tenantId = new TenantId(tenant);
        using var host = await _fixture.StartHostAsync(Enabled);
        Assert.Equal(BootstrapCommand.Success, (await RunBootstrapAsync(host, tenant, AuthApiFixture.NewEmail())).Code);

        var (code, output, error) = await RunEnableAsync(host, "--tenant-id", tenant.ToString(), "--module", "crm");

        Assert.Equal(EnableModuleCommand.Success, code);
        Assert.Empty(error);
        Assert.Contains("Default pipeline provisioned", output);

        await using var scope = host.Services.CreateAsyncScope();
        var crmContext = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        await using var transaction = await crmContext.Database.BeginTransactionAsync();
        await crmContext.SetTenantContextAsync(tenantId, CancellationToken.None);
        Assert.True(await crmContext.PipelineDefinitions.AnyAsync(p => p.TenantId == tenantId && p.Name == "Sales pipeline"));
    }

    [Fact]
    public async Task Bootstrap_with_modules_creates_the_administrator_and_enables_them_in_one_run()
    {
        var tenant = AuthApiFixture.NewTenantId();
        var email = AuthApiFixture.NewEmail();
        using var host = await _fixture.StartHostAsync(Enabled);
        using var client = host.CreateClient();

        var (code, output, error) = await RunBootstrapAsync(host, tenant, email, "--modules", "crm");

        Assert.Equal(BootstrapCommand.Success, code);
        Assert.Empty(error);
        Assert.Contains("Module 'crm' enabled", output);
        Assert.Equal(HttpStatusCode.Created, await CreateOpportunityAsync(client, await SignInAsync(client, output, email), await SeedPartyAsync(host, tenant)));
    }

    [Fact]
    public async Task Bootstrap_with_an_unknown_module_is_refused_before_anything_is_created()
    {
        var tenant = AuthApiFixture.NewTenantId();
        using var host = await _fixture.StartHostAsync(Enabled);

        var refused = await RunBootstrapAsync(host, tenant, AuthApiFixture.NewEmail(), "--modules", "crm,warehouse");

        Assert.Equal(BootstrapCommand.BadArguments, refused.Code);
        Assert.Empty(refused.Output);
        Assert.Contains("Unknown module 'warehouse'", refused.Error);
        // Nothing was created, so the tenant can still be bootstrapped.
        Assert.Equal(BootstrapCommand.Success, (await RunBootstrapAsync(host, tenant, AuthApiFixture.NewEmail())).Code);
    }

    private static async Task CreateCalendarEntryAsync(AuthApiHost host, long tenant, PrincipalRef principal)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var start = DateTimeOffset.UtcNow.AddHours(1);
        await scope.ServiceProvider.GetRequiredService<CreateCalendarEntryHandler>().HandleAsync(new CreateCalendarEntryCommand(
            new TenantId(tenant), principal, "Planning", null, "#3366cc", AllDay: false, start, start.AddHours(1),
            null, null, null, Guid.NewGuid().ToString(), Guid.NewGuid()));
    }

    private static async Task<int> CountCalendarEntriesAsync(AuthApiHost host, long tenant, PrincipalRef principal)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var entries = await scope.ServiceProvider.GetRequiredService<ListCalendarEntriesHandler>().HandleAsync(new ListCalendarEntriesQuery(
            new TenantId(tenant), principal, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2), Guid.NewGuid()));
        return entries.Count;
    }

    private static PrincipalRef PrincipalOf(string bearer)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(bearer);
        return new PrincipalRef(jwt.Issuer, jwt.Subject);
    }

    /// <summary>The generic enablement path covers Collaboration too: nothing about it is CRM-specific. Before the
    /// command the administrator holds no calendar grant; after it, the same administrator keeps a private calendar.</summary>
    [Fact]
    public async Task Enabling_collaboration_is_what_lets_a_bootstrapped_tenant_administrator_keep_a_calendar()
    {
        var tenant = AuthApiFixture.NewTenantId();
        var email = AuthApiFixture.NewEmail();
        using var host = await _fixture.StartHostAsync(Enabled);
        using var client = host.CreateClient();
        var bootstrap = await RunBootstrapAsync(host, tenant, email);
        Assert.Equal(BootstrapCommand.Success, bootstrap.Code);
        var principal = PrincipalOf(await SignInAsync(client, bootstrap.Output, email));

        await Assert.ThrowsAsync<CalendarEntryAuthorizationDeniedException>(() => CreateCalendarEntryAsync(host, tenant, principal));

        var (code, output, error) = await RunEnableAsync(host, "--tenant-id", tenant.ToString(), "--module", "collaboration");

        Assert.Equal(EnableModuleCommand.Success, code);
        Assert.Empty(error);
        Assert.Contains("Module 'collaboration' enabled", output);
        Assert.Contains("1 administrator role assignment", output);
        await CreateCalendarEntryAsync(host, tenant, principal);
        Assert.Equal(1, await CountCalendarEntriesAsync(host, tenant, principal));
    }

    [Fact]
    public async Task Bootstrap_with_modules_can_enable_collaboration_alongside_crm()
    {
        var tenant = AuthApiFixture.NewTenantId();
        var email = AuthApiFixture.NewEmail();
        using var host = await _fixture.StartHostAsync(Enabled);
        using var client = host.CreateClient();

        var (code, output, error) = await RunBootstrapAsync(host, tenant, email, "--modules", "crm,collaboration");

        Assert.Equal(BootstrapCommand.Success, code);
        Assert.Empty(error);
        Assert.Contains("Module 'crm' enabled", output);
        Assert.Contains("Module 'collaboration' enabled", output);
        var principal = PrincipalOf(await SignInAsync(client, output, email));
        await CreateCalendarEntryAsync(host, tenant, principal);
        Assert.Equal(1, await CountCalendarEntriesAsync(host, tenant, principal));
    }

    private static async Task<int> CountEnablementsAsync(AccessDbContext access, long tenant)
    {
        await using var transaction = await access.Database.BeginTransactionAsync();
        await access.SetTenantContextAsync(new TenantId(tenant), CancellationToken.None);
        return await access.TenantModuleEnablements.CountAsync(enablement => enablement.ModuleKey == "crm");
    }
}
