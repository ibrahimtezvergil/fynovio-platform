using Contracts;
using CRM.Customization;
using CRM.Domain;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application;

public sealed class ListOpportunitiesHandler(CrmDbContext context, IAccessScopeResolver scopeResolver, IAuthorizedPrincipalDirectory? principalDirectory = null)
{
    private const string ActionKeyValue = "crm.opportunity.list";

    public async Task<IReadOnlyList<OpportunitySummaryDto>> HandleAsync(ListOpportunitiesQuery query, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(query.TenantId, cancellationToken);

        var actor = new ActorContext(query.TenantId, query.Principal, query.CorrelationId);
        var scope = await scopeResolver.ResolveAsync(actor, new ActionKey(ActionKeyValue), nameof(Opportunity), cancellationToken);

        var tenantScoped = context.Opportunities.Where(o => o.TenantId == query.TenantId);
        IQueryable<Opportunity> filtered = scope switch
        {
            AccessScope.All => tenantScoped,
            AccessScope.AnyOf anyOf => ApplyAnyOf(tenantScoped, anyOf),
            _ => context.Opportunities.Where(_ => false) // AccessScope.None, and fail-closed on any future unrecognized case
        };

        if (query.Status is { } status)
            filtered = filtered.Where(o => o.Status == status);

        filtered = filtered.Where(o => o.IsArchived == query.ArchivedOnly);

        if (query.CustomFieldFilters is { Count: > 0 })
        {
            var definitions = await context.TenantFieldDefinitions.AsNoTracking()
                .Where(x => x.TenantId == query.TenantId && x.AggregateType == TenantFieldAggregateType.Opportunity)
                .ToListAsync(cancellationToken);
            var containment = CustomFieldFilter.ToContainmentJson(definitions, query.CustomFieldFilters);
            if (containment is not null)
                filtered = filtered.Where(o => o.CustomFields != null && EF.Functions.JsonContains(o.CustomFields, containment));
        }

        var rows = await filtered
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(o => new
            {
                Summary = new OpportunitySummaryDto(
                    o.Id, o.Status, o.EstimatedAmount, o.Currency, o.AssignedPrincipalIssuer, o.AssignedPrincipalSubject, o.PipelineStageId,
                    o.PartyRefPartyId, o.PipelineDefinitionVersionId, o.ExpiryDate, o.IsArchived, o.ArchivedAt, o.RowVersion,
                    o.CreatedAt, o.UpdatedAt, o.TotalAmount, Array.Empty<string>(), (string?)null),
                o.CustomFields
            })
            .ToListAsync(cancellationToken);
        var results = rows.Select(x => x.Summary with { CustomFields = CustomFieldValues.ToElement(x.CustomFields) }).ToList();

        var opportunityIds = results.Select(x => x.Id).ToArray();
        var needRows = await (from link in context.OpportunityNeeds
                              join need in context.CustomerNeeds on new { link.TenantId, link.CustomerNeedId } equals new { need.TenantId, CustomerNeedId = need.Id }
                              where link.TenantId == query.TenantId && opportunityIds.Contains(link.OpportunityId)
                              select new { link.OpportunityId, need.Name })
            .ToListAsync(cancellationToken);
        var needsByOpportunity = needRows.GroupBy(x => x.OpportunityId).ToDictionary(x => x.Key, x => (IReadOnlyList<string>)x.Select(n => n.Name).Order().ToArray());
        results = results.Select(x => x with { Needs = needsByOpportunity.GetValueOrDefault(x.Id, Array.Empty<string>()) }).ToList();
        if (principalDirectory is not null)
        {
            var principals = results.Select(x => new PrincipalRef(x.AssignedPrincipalIssuer, x.AssignedPrincipalSubject)).Distinct().ToArray();
            var names = await principalDirectory.ResolveDisplayNamesAsync(query.TenantId, principals, cancellationToken);
            results = results.Select(x => x with
            {
                AssignedPrincipalDisplayName = names.GetValueOrDefault(new PrincipalRef(x.AssignedPrincipalIssuer, x.AssignedPrincipalSubject))
            }).ToList();
        }

        await transaction.CommitAsync(cancellationToken);
        return results;
    }

    /// <summary>Phase 1.5 implements only ScopeTerm.OwnedBy (Contracts.ScopeTerm's own
    /// doc comment) — an AnyOf containing any other future term falls through to the
    /// fail-closed default, matching AccessScope's "an adapter that meets an
    /// unrecognized future ScopeTerm subtype must treat it as None" rule.</summary>
    private static IQueryable<Opportunity> ApplyAnyOf(IQueryable<Opportunity> source, AccessScope.AnyOf anyOf)
    {
        var ownedBy = anyOf.Terms.OfType<ScopeTerm.OwnedBy>().FirstOrDefault();
        if (ownedBy is null)
            return source.Where(_ => false);

        return source.Where(o => o.AssignedPrincipalIssuer == ownedBy.Principal.Issuer && o.AssignedPrincipalSubject == ownedBy.Principal.Subject);
    }
}
