using Contracts;
using CRM.Domain;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application;

public sealed class GetDefaultPipelineStagesHandler(CrmDbContext context, IAuthorizer authorizer)
{
    public async Task<IReadOnlyList<PipelineStageDto>> HandleAsync(GetDefaultPipelineStagesQuery query, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(query.TenantId, cancellationToken);
        var actor = new ActorContext(query.TenantId, query.Principal, query.CorrelationId);
        var decision = await authorizer.AuthorizeAsync(new AuthorizationRequest(actor, new ActionKey("crm.opportunity.read"),
            new ResourceDescriptor(nameof(Opportunity), null, null)), cancellationToken);
        if (!decision.IsAllowed)
            throw new OpportunityAuthorizationDeniedException("crm.opportunity.read", decision.ReasonCode, decision.DenialStage);

        var settings = await context.CrmSettings.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == query.TenantId, cancellationToken);
        var pipelineId = settings is null
            ? await context.PipelineDefinitions.AsNoTracking().Where(x => x.TenantId == query.TenantId && x.IsActive && !x.IsArchived)
                .OrderBy(x => x.Id).Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken)
            : settings.DefaultPipelineDefinitionId;
        if (pipelineId is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return [];
        }

        var versionId = await context.PipelineDefinitionVersions.AsNoTracking()
            .Where(x => x.TenantId == query.TenantId && x.PipelineDefinitionId == pipelineId && x.Status == PipelineVersionStatus.Published)
            .OrderByDescending(x => x.VersionNumber).Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken);
        IReadOnlyList<PipelineStageDto> stages = versionId is null ? [] : await context.PipelineStages.AsNoTracking()
            .Where(x => x.TenantId == query.TenantId && x.PipelineDefinitionVersionId == versionId && !x.IsArchived)
            .OrderBy(x => x.SortOrder)
            .Select(x => new PipelineStageDto(x.Id, x.Name, x.SortOrder, x.IsActive, x.IsEntry, x.IsArchived))
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return stages;
    }
}
