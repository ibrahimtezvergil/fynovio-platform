using Contracts;
using CRM.Customization;
using CRM.Domain;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application;

public sealed class GetOpportunityHandler(CrmDbContext context, IAuthorizer authorizer, ISemanticDefinitionReader definitionReader, ILinkTargetDirectory linkTargets, IAuthorizedPrincipalDirectory? principalDirectory = null)
{
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
        var decision = await authorizer.AuthorizeAsync(OpportunityReadAuthorization.Request(actor, opportunity), cancellationToken);

        string? assignedPrincipalDisplayName = null;
        if (decision.IsAllowed && principalDirectory is not null)
        {
            var principal = new PrincipalRef(opportunity.AssignedPrincipalIssuer, opportunity.AssignedPrincipalSubject);
            var names = await principalDirectory.ResolveDisplayNamesAsync(query.TenantId, [principal], cancellationToken);
            names.TryGetValue(principal, out assignedPrincipalDisplayName);
        }

        // Hydrated only after an allowed read decision, at this reader's own authorization.
        IReadOnlyDictionary<string, CustomFieldReferenceDto>? references = null;
        if (decision.IsAllowed && opportunity.CustomFields is not null)
        {
            var definitions = await definitionReader.ListFieldsAsync(query.TenantId, OpportunityFields.OwnerContext, OpportunityFields.ObjectType, cancellationToken);
            var hydrated = await CustomFieldReferences.HydrateAsync(linkTargets, actor, definitions, [(opportunity.Id, opportunity.CustomFields)], cancellationToken);
            references = hydrated.GetValueOrDefault(opportunity.Id);
        }

        await transaction.CommitAsync(cancellationToken);

        // Record-level denial looks identical to not-found — never 403 for "exists, not
        // yours" (architecture plan §15, binding spec §12's tenant-non-leak rule).
        return decision.IsAllowed ? OpportunityDto.From(opportunity) with { AssignedPrincipalDisplayName = assignedPrincipalDisplayName, CustomFieldReferences = references } : null;
    }
}
