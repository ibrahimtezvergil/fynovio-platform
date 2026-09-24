using Contracts;
using CRM.Domain;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application;

/// <summary>Every boolean here is (lifecycle-state guard from the aggregate) AND
/// (IAuthorizer.AuthorizeAsync for the corresponding ActionKey) — this is a UX aid
/// only (binding spec §13A); WinOpportunityHandler etc. re-check both independently
/// at execution time regardless of what this query said.</summary>
public sealed class GetOpportunityAvailableActionsHandler(CrmDbContext context, IAuthorizer authorizer)
{
    public async Task<OpportunityAvailableActionsDto?> HandleAsync(GetOpportunityAvailableActionsQuery query, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(query.TenantId, cancellationToken);

        var opportunity = await context.Opportunities.Include(o => o.Lines)
            .SingleOrDefaultAsync(o => o.Id == query.OpportunityId, cancellationToken);
        if (opportunity is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var actor = new ActorContext(query.TenantId, query.Principal, query.CorrelationId);
        var resource = new ResourceDescriptor(nameof(Opportunity), opportunity.Id, opportunity.AssignedPrincipal);

        var canOpen = !opportunity.IsArchived && opportunity.Status == OpportunityStatus.Draft
            && (await Authorize("crm.opportunity.open")).IsAllowed;
        var canChangeStage = !opportunity.IsArchived && opportunity.Status == OpportunityStatus.Open
            && (await Authorize("crm.opportunity.change_stage")).IsAllowed;
        var canWin = !opportunity.IsArchived && opportunity.Status == OpportunityStatus.Open
            && opportunity.Lines.Any(l => !l.IsOptional && !l.IsCanceled)
            && (await Authorize("crm.opportunity.win")).IsAllowed;
        var canLose = !opportunity.IsArchived && opportunity.Status is OpportunityStatus.Draft or OpportunityStatus.Open
            && (await Authorize("crm.opportunity.lose")).IsAllowed;
        var canReassign = opportunity.Status is not (OpportunityStatus.Won or OpportunityStatus.Lost)
            && (await Authorize("crm.opportunity.reassign")).IsAllowed;
        var canArchive = !opportunity.IsArchived && opportunity.Status is OpportunityStatus.Draft or OpportunityStatus.Open
            && (await Authorize("crm.opportunity.archive")).IsAllowed;
        var canRestore = opportunity.IsArchived
            && (await Authorize("crm.opportunity.restore")).IsAllowed;

        var allowedTargetStageIds = canChangeStage && opportunity.PipelineDefinitionVersionId is { } versionId
            ? await context.PipelineStages.AsNoTracking()
                .Where(s => s.PipelineDefinitionVersionId == versionId && s.IsActive && s.Id != opportunity.PipelineStageId)
                .Select(s => s.Id)
                .ToListAsync(cancellationToken)
            : [];

        await transaction.CommitAsync(cancellationToken);

        return new OpportunityAvailableActionsDto(canOpen, canChangeStage, allowedTargetStageIds, canWin, canLose, canReassign, canArchive, canRestore);

        Task<AuthorizationDecision> Authorize(string actionKey) =>
            authorizer.AuthorizeAsync(new AuthorizationRequest(actor, new ActionKey(actionKey), resource), cancellationToken);
    }
}
