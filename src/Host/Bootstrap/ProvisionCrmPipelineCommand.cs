using Access.Persistence;
using Contracts;
using CRM.Application;
using Microsoft.EntityFrameworkCore;

namespace Host.Bootstrap;

/// <summary>`dotnet Host.dll provision-crm-pipeline --tenant-id N --name "Sales pipeline" --stages "Qualification,Proposal,Won"` —
/// gives a bootstrapped tenant its first CRM pipeline (version 1). The operator names the stages; the first one is the
/// entry stage. Operator-only (never HTTP), gated by `Bootstrap:Enabled` like the other tenant commands. Idempotent:
/// for a tenant that already has a pipeline it exits 0 and changes nothing.</summary>
public static class ProvisionCrmPipelineCommand
{
    public const string Name = "provision-crm-pipeline";

    public const int Success = 0;
    public const int NotPermitted = BootstrapCommand.NotPermitted;
    public const int TenantNotBootstrapped = EnableModuleCommand.TenantNotBootstrapped;
    public const int BadArguments = BootstrapCommand.BadArguments;

    public static bool IsRequested(string[] args) => args.Length > 0 && args[0] == Name;

    public static async Task<int> RunAsync(
        IServiceProvider services,
        IConfiguration configuration,
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue("Bootstrap:Enabled", false))
        {
            await error.WriteLineAsync("Refused: set Bootstrap:Enabled=true (environment variable Bootstrap__Enabled=true) to run this command.");
            return NotPermitted;
        }

        var options = CommandOptions.Parse(args.Skip(1).ToArray());
        var stages = options.GetValueOrDefault("stages")?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList() ?? [];
        if (!long.TryParse(options.GetValueOrDefault("tenant-id"), out var tenantId) || tenantId <= 0
            || string.IsNullOrWhiteSpace(options.GetValueOrDefault("name")) || stages.Count == 0)
        {
            await error.WriteLineAsync($"Usage: {Name} --tenant-id <positive number> --name <pipeline name> --stages <stage>[,<stage>...]  (the first stage is the entry stage)");
            return BadArguments;
        }

        await using var scope = services.CreateAsyncScope();
        var tenant = new TenantId(tenantId);

        // A typo in --tenant-id must not quietly create configuration for a tenant that does not exist.
        var access = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        await using (var transaction = await access.Database.BeginTransactionAsync(cancellationToken))
        {
            await access.SetTenantContextAsync(tenant, cancellationToken);
            if (!await access.TenantAccessStates.AnyAsync(s => s.TenantId == tenant, cancellationToken))
            {
                await error.WriteLineAsync($"Refused: tenant {tenantId} is not bootstrapped; run bootstrap-tenant-admin first.");
                return TenantNotBootstrapped;
            }
        }

        try
        {
            var handler = scope.ServiceProvider.GetRequiredService<ProvisionPipelineHandler>();
            var result = await handler.HandleAsync(new ProvisionPipelineCommand(tenant, options["name"], stages), cancellationToken);
            await output.WriteLineAsync(result.Status == ProvisionPipelineStatus.Provisioned
                ? $"Pipeline '{options["name"]}' provisioned for tenant {tenantId}: {string.Join(" → ", stages)} (entry stage: {stages[0]})."
                : $"Tenant {tenantId} already has a pipeline; nothing changed.");
            return Success;
        }
        catch (ArgumentException ex)
        {
            await error.WriteLineAsync($"Refused: {ex.Message}");
            return BadArguments;
        }
    }
}
