using CRM.Domain;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application;

/// <summary>Gives a tenant its first sales pipeline (version 1) from operator-supplied stages. Idempotent by state:
/// once the tenant has any pipeline definition nothing is written, so a re-run never edits or duplicates it (and a
/// later change to the requested stages is never propagated). Operator-invoked and never over HTTP — it is tenant
/// configuration, not an Opportunity command, so it carries no PDP action.</summary>
public sealed class ProvisionPipelineHandler(CrmDbContext context)
{
    public const int MaxNameLength = 100;
    public const int MaxStages = 50;

    public async Task<ProvisionPipelineResult> HandleAsync(ProvisionPipelineCommand command, CancellationToken cancellationToken = default)
    {
        var (stageNames, retiredNames) = Validate(command);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(command.TenantId, cancellationToken);

        if (await context.PipelineDefinitions.AnyAsync(p => p.TenantId == command.TenantId, cancellationToken))
            return new ProvisionPipelineResult(ProvisionPipelineStatus.AlreadyProvisioned);

        var definition = PipelineDefinition.Create(command.TenantId, command.Name.Trim());
        context.PipelineDefinitions.Add(definition);
        await context.SaveChangesAsync(cancellationToken); // assigns definition.Id

        var version = definition.AddVersion(1);
        context.PipelineDefinitionVersions.Add(version);
        await context.SaveChangesAsync(cancellationToken); // assigns version.Id

        // AddStage marks the first stage as the entry stage; MarkEntry is never called, so the partial unique
        // index on the entry flag is only ever written once per row (see PipelineDefinitionVersion.MarkEntry).
        var sortOrder = 10;
        foreach (var name in stageNames)
        {
            context.PipelineStages.Add(version.AddStage(name, sortOrder));
            sortOrder += 10;
        }

        foreach (var name in retiredNames)
        {
            var retired = version.AddStage(name, sortOrder);
            retired.Deactivate();
            context.PipelineStages.Add(retired);
            sortOrder += 10;
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ProvisionPipelineResult(ProvisionPipelineStatus.Provisioned, version.Id);
    }

    private static (List<string> Active, List<string> Retired) Validate(ProvisionPipelineCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Trim().Length > MaxNameLength)
            throw new ArgumentException($"A pipeline name of 1–{MaxNameLength} characters is required.", nameof(command));

        var active = command.StageNames.Select(n => n?.Trim() ?? string.Empty).ToList();
        var retired = (command.RetiredStageNames ?? []).Select(n => n?.Trim() ?? string.Empty).ToList();
        var all = active.Concat(retired).ToList();
        if (active.Count == 0 || all.Count > MaxStages)
            throw new ArgumentException($"Between 1 and {MaxStages} stages, at least one of them active, are required.", nameof(command));
        if (all.Any(n => n.Length is 0 or > MaxNameLength))
            throw new ArgumentException($"Every stage needs a name of 1–{MaxNameLength} characters.", nameof(command));
        if (all.Distinct(StringComparer.OrdinalIgnoreCase).Count() != all.Count)
            throw new ArgumentException("Stage names must be unique (ignoring case).", nameof(command));

        return (active, retired);
    }
}
