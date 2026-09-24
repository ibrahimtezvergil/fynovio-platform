using Contracts;
using CRM.Domain;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application;

public sealed class ListOpportunitiesHandler(CrmDbContext context, IAccessScopeResolver scopeResolver)
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

        var results = await filtered
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(o => new OpportunitySummaryDto(
                o.Id, o.Status, o.EstimatedAmount, o.Currency, o.AssignedPrincipalIssuer, o.AssignedPrincipalSubject, o.PipelineStageId,
                o.PartyRefPartyId, o.PipelineDefinitionVersionId, o.ExpiryDate, o.IsArchived, o.ArchivedAt, o.RowVersion))
            .ToListAsync(cancellationToken);

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
