using Access.Persistence;
using Contracts;
using CRM.Application;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Host.Bootstrap;

/// <summary>`dotnet Host.dll backfill-crm-pipelines` — a one-time operator command with two
/// idempotent sub-steps, run once each in the order the design doc requires
/// (docs/architecture-analysis/2026-09-27-crm-opportunity-pipeline-won-lost-stage-integration.md
/// §6.2): (1) provision a default pipeline for any CRM-enabled tenant that still has none
/// (closes the gap Task 8's auto-provision doesn't cover retroactively), then (2, added in
/// a later task) backfill Won/Lost stages and stage-less opportunities. Safe to re-run —
/// each sub-step only acts on tenants/rows still needing it.</summary>
public static class BackfillCrmPipelinesCommand
{
    public const string Name = "backfill-crm-pipelines";
    public const int Success = 0;
    public const int NotPermitted = BootstrapCommand.NotPermitted;

    public static bool IsRequested(string[] args) => args.Length > 0 && args[0] == Name;

    public static async Task<int> RunAsync(
        IServiceProvider services, IConfiguration configuration, string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue("Bootstrap:Enabled", false))
        {
            await error.WriteLineAsync("Refused: set Bootstrap:Enabled=true (environment variable Bootstrap__Enabled=true) to run this command.");
            return NotPermitted;
        }

        await using var scope = services.CreateAsyncScope();
        var provisioned = await ProvisionMissingPipelinesAsync(scope.ServiceProvider, cancellationToken);
        await output.WriteLineAsync($"Provisioned a default pipeline for {provisioned} tenant(s) that had none.");
        return Success;
    }

    private static async Task<int> ProvisionMissingPipelinesAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var accessContext = scopedServices.GetRequiredService<Access.Persistence.AccessDbContext>();
        var pipelineHandler = scopedServices.GetRequiredService<ProvisionPipelineHandler>();
        var crmContext = scopedServices.GetRequiredService<CrmDbContext>();

        // Get all bootstrapped tenants
        var allTenants = await accessContext.TenantAccessStates
            .Select(s => s.TenantId)
            .ToListAsync(cancellationToken);

        var provisionedCount = 0;
        foreach (var tenantId in allTenants)
        {
            // Check if this tenant has CRM enabled and no pipeline
            await using var checkTransaction = await accessContext.Database.BeginTransactionAsync(cancellationToken);
            await accessContext.SetTenantContextAsync(tenantId, cancellationToken);
            var hasCrmEnabled = await accessContext.TenantModuleEnablements
                .AnyAsync(m => m.ModuleKey == "crm", cancellationToken);
            await checkTransaction.RollbackAsync(cancellationToken);

            if (!hasCrmEnabled)
            {
                continue;
            }

            // Check if it already has a pipeline
            await using var pipelineTransaction = await crmContext.Database.BeginTransactionAsync(cancellationToken);
            await crmContext.SetTenantContextAsync(tenantId, cancellationToken);
            var alreadyHasOne = await crmContext.PipelineDefinitions.AnyAsync(p => p.TenantId == tenantId, cancellationToken);
            await pipelineTransaction.RollbackAsync(cancellationToken);

            if (alreadyHasOne)
            {
                continue;
            }

            // Provision the pipeline
            var result = await pipelineHandler.HandleAsync(
                new ProvisionPipelineCommand(tenantId, CrmDefaultPipelineSeed.PipelineName, CrmDefaultPipelineSeed.ActiveStageNames, RetiredStageNames: []),
                cancellationToken);
            if (result.Status == ProvisionPipelineStatus.Provisioned)
            {
                provisionedCount++;
            }
        }
        return provisionedCount;
    }
}
