using Contracts;
using CRM.Domain;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application;

/// <summary>The candidates the UI may offer for `Reassign` on ONE opportunity. Authorization-aware twice over:
/// only an actor who may reassign THIS record gets an answer (`crm.opportunity.reassign` on the resource), and
/// the candidates are exactly the members Access reports as permitted every action in
/// <see cref="CrmAssignmentPolicy"/>. `ReassignOpportunityHandler` re-checks the chosen target with the same
/// predicate, so this list is guidance, never the enforcement.</summary>
public sealed class ListAssignablePrincipalsHandler(
    CrmDbContext context,
    IAuthorizer authorizer,
    IAuthorizedPrincipalDirectory directory)
{
    private const string ActionKeyValue = CrmActionKeys.OpportunityReassign;
    public const int DefaultTake = 25;

    public async Task<IReadOnlyList<AssignablePrincipalDto>> HandleAsync(ListAssignablePrincipalsQuery query, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(query.TenantId, cancellationToken);

        var opportunity = await context.Opportunities.AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == query.OpportunityId, cancellationToken)
            ?? throw new OpportunityNotFoundException(query.OpportunityId);

        var actor = new ActorContext(query.TenantId, query.Principal, query.CorrelationId);
        var resource = new ResourceDescriptor(nameof(Opportunity), opportunity.Id, opportunity.AssignedPrincipal);
        var decision = await authorizer.AuthorizeAsync(
            new AuthorizationRequest(actor, new ActionKey(ActionKeyValue), resource), cancellationToken);
        if (!decision.IsAllowed)
            throw new OpportunityAuthorizationDeniedException(ActionKeyValue, decision.ReasonCode, decision.DenialStage, opportunity.Id);

        await transaction.CommitAsync(cancellationToken);

        // A closed opportunity cannot be reassigned at all, so nobody is assignable.
        if (opportunity.Status is OpportunityStatus.Won or OpportunityStatus.Lost)
            return [];

        var permitted = await directory.ListPermittedPrincipalsAsync(
            query.TenantId, CrmAssignmentPolicy.RequiredAssigneeActions, query.Search, query.Take <= 0 ? DefaultTake : query.Take, cancellationToken);

        // Reassigning to the current owner changes nothing.
        return permitted
            .Where(p => p.Principal != opportunity.AssignedPrincipal)
            .Select(p => new AssignablePrincipalDto(p.Principal.Issuer, p.Principal.Subject, p.DisplayName, p.Email))
            .ToList();
    }
}
