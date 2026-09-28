using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
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

/// <summary>Phase 2.6 gap P1 — the production path that gives a tenant its first sales pipeline: an operator command
/// that takes the stages from the operator (no built-in stage template).</summary>
[Collection(HostIntegrationCollection.Name)]
public sealed class ProvisionCrmPipelineCommandTests : IClassFixture<AuthApiFixture>
{
    private static readonly Dictionary<string, string?> Enabled = new() { ["Bootstrap__Enabled"] = "true" };

    private readonly AuthApiFixture _fixture;

    public ProvisionCrmPipelineCommandTests(AuthApiFixture fixture) => _fixture = fixture;

    private static async Task<(int Code, string Output, string Error)> RunAsync(AuthApiHost host, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await ProvisionCrmPipelineCommand.RunAsync(
            host.Services, host.Services.GetRequiredService<IConfiguration>(), [ProvisionCrmPipelineCommand.Name, .. args], output, error);
        return (code, output.ToString(), error.ToString());
    }

    private static async Task<(int Code, string Output)> BootstrapAsync(AuthApiHost host, long tenant, string email, params string[] extra)
    {
        var output = new StringWriter();
        var code = await BootstrapCommand.RunAsync(
            host.Services, host.Services.GetRequiredService<IConfiguration>(),
            [BootstrapCommand.Name, "--tenant-id", tenant.ToString(), "--email", email, "--display-name", "Ops Admin", .. extra], output, new StringWriter());
        return (code, output.ToString());
    }

    private static async Task<int> StageCountAsync(AuthApiHost host, long tenant)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var crm = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        await using var transaction = await crm.Database.BeginTransactionAsync();
        await crm.SetTenantContextAsync(new TenantId(tenant));
        return await crm.PipelineStages.CountAsync(s => s.TenantId == new TenantId(tenant));
    }

    [Fact]
    public void The_command_is_recognised_only_by_its_exact_name()
    {
        Assert.True(ProvisionCrmPipelineCommand.IsRequested([ProvisionCrmPipelineCommand.Name]));
        Assert.False(ProvisionCrmPipelineCommand.IsRequested([]));
        Assert.False(ProvisionCrmPipelineCommand.IsRequested(["provision"]));
    }

    [Fact]
    public async Task It_refuses_to_run_unless_bootstrap_is_explicitly_enabled()
    {
        using var host = await _fixture.StartHostAsync();

        var (code, output, error) = await RunAsync(host, "--tenant-id", "5", "--name", "Sales", "--stages", "A,B");

        Assert.Equal(ProvisionCrmPipelineCommand.NotPermitted, code);
        Assert.Empty(output);
        Assert.Contains("Bootstrap:Enabled", error);
    }

    [Theory]
    [InlineData("--name", "Sales", "--stages", "A")]
    [InlineData("--tenant-id", "0", "--name", "Sales", "--stages", "A")]
    [InlineData("--tenant-id", "5", "--stages", "A")]
    [InlineData("--tenant-id", "5", "--name", "Sales")]
    [InlineData("--tenant-id", "5", "--name", "Sales", "--stages", " , ")]
    public async Task Missing_or_malformed_arguments_print_usage(params string[] args)
    {
        using var host = await _fixture.StartHostAsync(Enabled);

        var (code, output, error) = await RunAsync(host, args);

        Assert.Equal(ProvisionCrmPipelineCommand.BadArguments, code);
        Assert.Empty(output);
        Assert.Contains("Usage", error);
    }

    [Fact]
    public async Task A_tenant_that_was_never_bootstrapped_gets_nothing()
    {
        var tenant = AuthApiFixture.NewTenantId();
        using var host = await _fixture.StartHostAsync(Enabled);

        var (code, _, error) = await RunAsync(host, "--tenant-id", tenant.ToString(), "--name", "Sales", "--stages", "A,B");

        Assert.Equal(ProvisionCrmPipelineCommand.TenantNotBootstrapped, code);
        Assert.Contains("bootstrap-tenant-admin", error);
        Assert.Equal(0, await StageCountAsync(host, tenant));
    }

    [Fact]
    public async Task Duplicate_stage_names_are_refused_with_the_reason()
    {
        var tenant = AuthApiFixture.NewTenantId();
        using var host = await _fixture.StartHostAsync(Enabled);
        Assert.Equal(BootstrapCommand.Success, (await BootstrapAsync(host, tenant, AuthApiFixture.NewEmail())).Code);

        var (code, _, error) = await RunAsync(host, "--tenant-id", tenant.ToString(), "--name", "Sales", "--stages", "A,a");

        Assert.Equal(ProvisionCrmPipelineCommand.BadArguments, code);
        Assert.Contains("unique", error);
        Assert.Equal(0, await StageCountAsync(host, tenant));
    }

    [Fact]
    public async Task Running_it_again_is_a_no_op_that_still_succeeds()
    {
        var tenant = AuthApiFixture.NewTenantId();
        using var host = await _fixture.StartHostAsync(Enabled);
        Assert.Equal(BootstrapCommand.Success, (await BootstrapAsync(host, tenant, AuthApiFixture.NewEmail())).Code);
        Assert.Equal(ProvisionCrmPipelineCommand.Success, (await RunAsync(host, "--tenant-id", tenant.ToString(), "--name", "Sales", "--stages", "A,B")).Code);

        var (code, output, _) = await RunAsync(host, "--tenant-id", tenant.ToString(), "--name", "Sales", "--stages", "X,Y,Z");

        Assert.Equal(ProvisionCrmPipelineCommand.Success, code);
        Assert.Contains("already has a pipeline", output);
        Assert.Equal(4, await StageCountAsync(host, tenant));
    }

    /// <summary>The point of P1: a tenant set up the production way (bootstrap + custom pipeline provisioning + CRM module) — no seed —
    /// opens an opportunity into the operator's entry stage and moves it to the next stage. The custom pipeline must be provisioned
    /// BEFORE enabling the CRM module, so it doesn't get overridden by the auto-provisioned default.</summary>
    [Fact]
    public async Task A_production_provisioned_tenant_can_open_an_opportunity_into_its_entry_stage_and_move_it_on()
    {
        var tenant = AuthApiFixture.NewTenantId();
        var email = AuthApiFixture.NewEmail();
        using var host = await _fixture.StartHostAsync(Enabled);
        using var client = host.CreateClient();

        // Bootstrap WITHOUT CRM enabled — we'll enable it after provisioning the custom pipeline
        var bootstrap = await BootstrapAsync(host, tenant, email);
        Assert.Equal(BootstrapCommand.Success, bootstrap.Code);

        // Provision custom stages BEFORE enabling CRM — so it's the sole pipeline, not overridden by the auto-provisioned default
        var (code, output, error) = await RunAsync(host, "--tenant-id", tenant.ToString(), "--name", "Sales", "--stages", "Lead,Quote,Contract");
        Assert.Equal(ProvisionCrmPipelineCommand.Success, code);
        Assert.Empty(error);
        Assert.Contains("entry stage: Lead", output);

        // Enable CRM after the custom pipeline already exists — Task 8's auto-provisioning must be a no-op here
        // (this is the documented, intended order: provision custom stages BEFORE enabling the module, if you want them
        // instead of the generic default).
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var enableExitCode = await EnableModuleCommand.EnableAsync(scope.ServiceProvider, new TenantId(tenant), "crm", new StringWriter(), new StringWriter(), CancellationToken.None);
            Assert.Equal(EnableModuleCommand.Success, enableExitCode);
        }

        var resetToken = Uri.UnescapeDataString(Regex.Match(bootstrap.Output, "#token=(\\S+)").Groups[1].Value);
        Assert.Equal(HttpStatusCode.NoContent, (await Reset(client, resetToken, NewPassword)).StatusCode);
        var login = await Login(client, email, NewPassword);
        var bearer = (await Json(login)).GetProperty("accessToken").GetString()!;

        long partyId;
        await using (var scope = host.Services.CreateAsyncScope())
        {
            partyId = (await scope.ServiceProvider.GetRequiredService<MasterData.Application.CreatePartyHandler>().HandleAsync(
                new MasterData.Application.CreatePartyCommand(new TenantId(tenant), PartyType.Organization, "Acme", null, null, null, Guid.NewGuid().ToString(), Guid.NewGuid()))).PartyId;
        }

        HttpRequestMessage Post(string path, object body)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            return request;
        }

        var created = await client.SendAsync(Post("/opportunities", new { partyId, currency = "TRY", estimatedAmount = 100 }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await Json(created)).GetProperty("opportunityId").GetInt64();
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Post($"/opportunities/{id}/open", new { expectedVersion = 1, expiryDate = DateTimeOffset.UtcNow.AddDays(30) }))).StatusCode);

        var detailRequest = new HttpRequestMessage(HttpMethod.Get, $"/opportunities/{id}");
        detailRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        var detail = await Json(await client.SendAsync(detailRequest));
        var versionId = detail.GetProperty("pipelineDefinitionVersionId").GetInt64();
        var entryStageId = detail.GetProperty("pipelineStageId").GetInt64();

        var stagesRequest = new HttpRequestMessage(HttpMethod.Get, $"/pipelines/{versionId}/stages");
        stagesRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        var stages = (await Json(await client.SendAsync(stagesRequest))).EnumerateArray().ToList();
        Assert.Equal(["Lead", "Quote", "Contract", "Won", "Lost"], stages.Select(s => s.GetProperty("name").GetString()));
        Assert.Equal(entryStageId, stages[0].GetProperty("id").GetInt64());

        var moved = await client.SendAsync(Post($"/opportunities/{id}/stage",
            new { expectedVersion = detail.GetProperty("rowVersion").GetInt64(), targetStageId = stages[1].GetProperty("id").GetInt64() }));
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
    }
}
