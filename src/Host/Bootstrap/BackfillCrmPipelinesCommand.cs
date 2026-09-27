using Contracts;
using CRM.Application;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Host.Bootstrap;

/// <summary>`dotnet Host.dll backfill-crm-pipelines --tenant-ids "1,2,3"` — a one-time operator
/// command with two idempotent sub-steps, run once each in the order the design doc requires
/// (docs/architecture-analysis/2026-09-27-crm-opportunity-pipeline-won-lost-stage-integration.md
/// §6.2): (1) provision a default pipeline for any of the given CRM-enabled tenants that still
/// has none (closes the gap Task 8's auto-provision doesn't cover retroactively), then (2, added
/// in a later task) backfill Won/Lost stages and stage-less opportunities for the same tenants.
/// Safe to re-run — each sub-step only acts on rows still needing it. Takes explicit tenant IDs,
/// not auto-discovery: every tenant-scoped table has FORCE ROW LEVEL SECURITY and the app's
/// runtime role cannot bypass it, so there is no way to enumerate "every tenant" from inside this
/// process — the operator (who created every tenant via bootstrap-tenant-admin) supplies the
/// list, same as every other bootstrap command in this codebase already requires.</summary>
public static class BackfillCrmPipelinesCommand
{
    public const string Name = "backfill-crm-pipelines";
    public const int Success = 0;
    public const int NotPermitted = BootstrapCommand.NotPermitted;
    public const int BadArguments = 64;

    public static bool IsRequested(string[] args) => args.Length > 0 && args[0] == Name;

    public static async Task<int> RunAsync(
        IServiceProvider services, IConfiguration configuration, string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue("Bootstrap:Enabled", false))
        {
            await error.WriteLineAsync("Refused: set Bootstrap:Enabled=true (environment variable Bootstrap__Enabled=true) to run this command.");
            return NotPermitted;
        }

        var tenantIds = ParseTenantIds(args);
        if (tenantIds is null || tenantIds.Count == 0)
        {
            await error.WriteLineAsync("Usage: backfill-crm-pipelines --tenant-ids \"1,2,3\"");
            return BadArguments;
        }

        await using var scope = services.CreateAsyncScope();
        var provisioned = 0;
        foreach (var tenantId in tenantIds)
        {
            provisioned += await ProvisionMissingPipelineAsync(scope.ServiceProvider, tenantId, cancellationToken);
        }

        await output.WriteLineAsync($"Provisioned a default pipeline for {provisioned} tenant(s) that had none.");
        return Success;
    }

    private static List<TenantId>? ParseTenantIds(string[] args)
    {
        for (var i = 1; i < args.Length - 1; i++)
        {
            if (args[i] != "--tenant-ids") continue;
            var raw = args[i + 1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var ids = new List<TenantId>();
            foreach (var token in raw)
            {
                if (!long.TryParse(token, out var value) || value <= 0) return null;
                ids.Add(new TenantId(value));
            }
            return ids.Count == 0 ? null : ids;
        }
        return null;
    }

    private static async Task<int> ProvisionMissingPipelineAsync(
        IServiceProvider scopedServices, TenantId tenantId, CancellationToken cancellationToken)
    {
        var pipelineHandler = scopedServices.GetRequiredService<ProvisionPipelineHandler>();
        var crmContext = scopedServices.GetRequiredService<CrmDbContext>();

        // The handler manages its own transaction; we just need to check if already provisioned
        await using var transaction = await crmContext.Database.BeginTransactionAsync(cancellationToken);
        await crmContext.SetTenantContextAsync(tenantId, cancellationToken);
        var alreadyHasOne = await crmContext.PipelineDefinitions.AnyAsync(p => p.TenantId == tenantId, cancellationToken);
        await transaction.RollbackAsync(cancellationToken);

        if (alreadyHasOne) return 0;

        var result = await pipelineHandler.HandleAsync(
            new ProvisionPipelineCommand(tenantId, CrmDefaultPipelineSeed.PipelineName, CrmDefaultPipelineSeed.ActiveStageNames, RetiredStageNames: []),
            cancellationToken);
        return result.Status == ProvisionPipelineStatus.Provisioned ? 1 : 0;
    }
}
