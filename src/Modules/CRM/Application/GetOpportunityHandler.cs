using Contracts;
using CRM.Domain;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application;

public sealed class GetOpportunityHandler(CrmDbContext context, IAuthorizer authorizer)
{
    private const string ActionKeyValue = "crm.opportunity.read";

    public async Task<OpportunityDto?> HandleAsync(GetOpportunityQuery query, CancellationToken cancellationToken = default)
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
        var decision = await authorizer.AuthorizeAsync(
            new AuthorizationRequest(actor, new ActionKey(ActionKeyValue), resource), cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        // Record-level denial looks identical to not-found — never 403 for "exists, not
        // yours" (architecture plan §15, binding spec §12's tenant-non-leak rule).
        return decision.IsAllowed ? OpportunityDto.From(opportunity) : null;
    }
}
