using Contracts;
using CRM.Domain;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application;

public sealed class ValidatePipelineDraftHandler(CrmDbContext context, IAuthorizer authorizer)
{
    public async Task<PipelineImpactValidationDto> HandleAsync(TenantId tenantId, long pipelineId, long versionId,
        PrincipalRef principal, Guid correlationId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(tenantId, cancellationToken);
        await GetCrmSettingsHandler.AuthorizeAsync(tenantId, principal, correlationId, CrmActionKeys.SettingsRead, authorizer, cancellationToken);
        var version = await context.PipelineDefinitionVersions.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.PipelineDefinitionId == pipelineId && x.Id == versionId, cancellationToken)
            ?? throw new KeyNotFoundException("Pipeline version was not found.");
        var stages = await context.PipelineStages.AsNoTracking().Where(x => x.TenantId == tenantId && x.PipelineDefinitionVersionId == versionId).ToListAsync(cancellationToken);
        var transitions = await context.PipelineStageTransitions.AsNoTracking().Where(x => x.TenantId == tenantId && x.PipelineDefinitionVersionId == versionId).ToListAsync(cancellationToken);
        var errors = PublishPipelineVersionHandler.Validate(version, stages, transitions);
        var publishedIds = await context.PipelineDefinitionVersions.AsNoTracking().Where(x => x.TenantId == tenantId && x.PipelineDefinitionId == pipelineId && x.Status == PipelineVersionStatus.Published).Select(x => x.Id).ToArrayAsync(cancellationToken);
        var impact = publishedIds.Length == 0 ? 0 : await context.Opportunities.LongCountAsync(x => x.TenantId == tenantId && x.PipelineDefinitionVersionId != null && publishedIds.Contains(x.PipelineDefinitionVersionId.Value), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PipelineImpactValidationDto(errors.Count == 0, version.Id, stages.Count(x => x.IsActive && !x.IsArchived), stages.Count(x => x.IsEntry && x.IsActive && !x.IsArchived), errors.Count, checked((int)Math.Min(impact, int.MaxValue)), errors);
    }
}
