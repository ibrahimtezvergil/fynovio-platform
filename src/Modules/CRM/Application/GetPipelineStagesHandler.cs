using Contracts;
using CRM.Domain;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application;

public sealed class GetPipelineStagesHandler(CrmDbContext context, IAuthorizer authorizer)
{
    private const string ActionKeyValue = "crm.opportunity.read";

    public async Task<IReadOnlyList<PipelineStageDto>> HandleAsync(GetPipelineStagesQuery query, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(query.TenantId, cancellationToken);

        // Coarse-only check, like CreateOpportunityHandler's CREATE-shaped resource:
        // pipeline stages are tenant-wide configuration, not a specific Opportunity
        // record, so there is no owner to evaluate an owner-relation grant against
        // (test-gap audit §7, 2026-09-19).
        var actor = new ActorContext(query.TenantId, query.Principal, query.CorrelationId);
        var resource = new ResourceDescriptor(nameof(Opportunity), null, null);
        var decision = await authorizer.AuthorizeAsync(
            new AuthorizationRequest(actor, new ActionKey(ActionKeyValue), resource), cancellationToken);
        if (!decision.IsAllowed)
            throw new OpportunityAuthorizationDeniedException(ActionKeyValue, decision.ReasonCode, decision.DenialStage);

        var results = await context.PipelineStages
            .Where(s => s.PipelineDefinitionVersionId == query.PipelineDefinitionVersionId)
            .OrderBy(s => s.SortOrder)
            .Select(s => new PipelineStageDto(s.Id, s.Name, s.SortOrder, s.IsActive, s.IsEntry, s.IsArchived, s.Kind))
            .ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return results;
    }
}
