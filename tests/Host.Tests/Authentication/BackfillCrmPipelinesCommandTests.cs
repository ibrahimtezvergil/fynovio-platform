using Access.Persistence;
using Contracts;
using CRM.Persistence;
using Host.Bootstrap;
using Host.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Host.Tests.Authentication;

/// <summary>Task 9 — one-time retroactive provisioning for tenants enabled before Task 8 shipped.
/// A tenant that was CRM-enabled but has no pipeline gets one from this command (idempotent).</summary>
[Collection(HostIntegrationCollection.Name)]
public sealed class BackfillCrmPipelinesCommandTests : IClassFixture<AuthApiFixture>
{
    private static readonly Dictionary<string, string?> Enabled = new() { ["Bootstrap__Enabled"] = "true" };

    private readonly AuthApiFixture _fixture;

    public BackfillCrmPipelinesCommandTests(AuthApiFixture fixture) => _fixture = fixture;

    private static async Task<(int Code, string Output, string Error)> RunBackfillAsync(AuthApiHost host, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await BackfillCrmPipelinesCommand.RunAsync(
            host.Services, host.Services.GetRequiredService<IConfiguration>(), [BackfillCrmPipelinesCommand.Name, .. args], output, error);
        return (code, output.ToString(), error.ToString());
    }

    private static async Task<(int Code, string Output)> BootstrapAsync(AuthApiHost host, long tenant, string email)
    {
        var output = new StringWriter();
        var code = await BootstrapCommand.RunAsync(
            host.Services, host.Services.GetRequiredService<IConfiguration>(),
            [BootstrapCommand.Name, "--tenant-id", tenant.ToString(), "--email", email, "--display-name", "Ops Admin"], output, new StringWriter());
        return (code, output.ToString());
    }

    private static async Task EnableCrmAsync(AuthApiHost host, long tenant)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var exitCode = await EnableModuleCommand.EnableAsync(scope.ServiceProvider, new TenantId(tenant), "crm", new StringWriter(), new StringWriter(), CancellationToken.None);
        Assert.Equal(EnableModuleCommand.Success, exitCode);
    }

    [Fact]
    public void The_command_is_recognised_only_by_its_exact_name()
    {
        Assert.True(BackfillCrmPipelinesCommand.IsRequested([BackfillCrmPipelinesCommand.Name]));
        Assert.False(BackfillCrmPipelinesCommand.IsRequested([]));
        Assert.False(BackfillCrmPipelinesCommand.IsRequested(["backfill"]));
    }

    [Fact]
    public async Task It_refuses_to_run_unless_bootstrap_is_explicitly_enabled()
    {
        using var host = await _fixture.StartHostAsync();

        var (code, output, error) = await RunBackfillAsync(host);

        Assert.Equal(BackfillCrmPipelinesCommand.NotPermitted, code);
        Assert.Empty(output);
        Assert.Contains("Bootstrap:Enabled", error);
    }

    [Fact]
    public async Task RunAsync_provisions_a_pipeline_for_a_crm_enabled_tenant_with_none()
    {
        var tenant = AuthApiFixture.NewTenantId();
        var tenantId = new TenantId(tenant);
        using var host = await _fixture.StartHostAsync(Enabled);

        // Bootstrap the tenant
        Assert.Equal(BootstrapCommand.Success, (await BootstrapAsync(host, tenant, AuthApiFixture.NewEmail())).Code);

        // Manually enable CRM module (without auto-provisioning a pipeline in this test)
        // We do this by directly inserting into TenantModuleEnablements
        await using var scope = host.Services.CreateAsyncScope();
        var accessContext = scope.ServiceProvider.GetRequiredService<Access.Persistence.AccessDbContext>();
        await using var transaction = await accessContext.Database.BeginTransactionAsync();
        await accessContext.SetTenantContextAsync(tenantId, CancellationToken.None);
        // Manually insert a CRM module enablement
        await accessContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO access.tenant_module_enablements (tenant_id, module_key, template_version, enabled_at) VALUES ({tenantId.Value}, 'crm', 1, {DateTimeOffset.UtcNow})");
        await transaction.CommitAsync();

        // Verify the pipeline does not exist
        await using var scope2 = host.Services.CreateAsyncScope();
        var crmContext2 = scope2.ServiceProvider.GetRequiredService<CrmDbContext>();
        await using var transaction2 = await crmContext2.Database.BeginTransactionAsync();
        await crmContext2.SetTenantContextAsync(tenantId, CancellationToken.None);
        Assert.False(await crmContext2.PipelineDefinitions.AnyAsync(p => p.TenantId == tenantId));

        // Now run the backfill command
        var (exitCode, output, error) = await RunBackfillAsync(host);

        // Verify success and that a pipeline was provisioned
        Assert.Equal(BackfillCrmPipelinesCommand.Success, exitCode);
        Assert.Empty(error);
        Assert.Contains("Provisioned a default pipeline for 1 tenant(s)", output);

        // Verify the pipeline was created
        await using var scope3 = host.Services.CreateAsyncScope();
        var crmContext3 = scope3.ServiceProvider.GetRequiredService<CrmDbContext>();
        await using var transaction3 = await crmContext3.Database.BeginTransactionAsync();
        await crmContext3.SetTenantContextAsync(tenantId, CancellationToken.None);
        Assert.True(await crmContext3.PipelineDefinitions.AnyAsync(p => p.TenantId == tenantId));
    }

    [Fact]
    public async Task Running_it_again_is_a_no_op_that_still_succeeds()
    {
        var tenant = AuthApiFixture.NewTenantId();
        using var host = await _fixture.StartHostAsync(Enabled);

        // Bootstrap and enable CRM
        Assert.Equal(BootstrapCommand.Success, (await BootstrapAsync(host, tenant, AuthApiFixture.NewEmail())).Code);
        await EnableCrmAsync(host, tenant);

        // First run should find and provision 1 (but since it already exists from enable, it will be 0)
        var (code1, output1, _) = await RunBackfillAsync(host);
        Assert.Equal(BackfillCrmPipelinesCommand.Success, code1);
        Assert.Contains("Provisioned a default pipeline for 0 tenant(s)", output1);

        // Second run should still succeed and still find 0
        var (code2, output2, _) = await RunBackfillAsync(host);
        Assert.Equal(BackfillCrmPipelinesCommand.Success, code2);
        Assert.Contains("Provisioned a default pipeline for 0 tenant(s)", output2);
    }
}
