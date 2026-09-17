using Access.Domain.Authorization;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Application;

/// <summary>Phase 1.5 implements exactly three outcomes: `None`, `All`, or
/// `AnyOf([OwnedBy])` (round 3 §8 freeze #10). The critical invariant
/// (round 3 §5) is verified by AuthorizeResolveAccessScopeEquivalenceTests.</summary>
public sealed class AccessScopeResolver(
    AccessDbContext context,
    PrincipalResolver principalResolver,
    IActionCatalog actionCatalog) : IAccessScopeResolver
{
    public async Task<AccessScope> ResolveAsync(ActorContext actor, ActionKey action, string resourceType, CancellationToken cancellationToken = default)
    {
        if (!await actionCatalog.IsActiveAsync(action, cancellationToken))
            return new AccessScope.None();

        var accountId = await principalResolver.ResolveAccountIdAsync(actor.Principal, cancellationToken);
        if (accountId is null)
            return new AccessScope.None();

        var now = DateTimeOffset.UtcNow;
        var items = await context.RoleAssignments
            .Where(a => a.TenantId == actor.TenantId && a.AccountId == accountId
                && a.ValidFrom <= now && (a.ValidTo == null || now < a.ValidTo))
            .Join(context.RolePermissionSets, a => a.RoleId, rps => rps.RoleId, (a, rps) => rps.PermissionSetId)
            .Join(context.PermissionSetItems, psId => psId, i => i.PermissionSetId, (psId, i) => i)
            .Where(i => i.ActionKey == action.Value)
            .ToListAsync(cancellationToken);

        if (items.Any(i => i.Relation is null))
            return new AccessScope.All();

        if (items.Any(i => i.Relation == PermissionSetItem.OwnerRelation))
            return new AccessScope.AnyOf([new ScopeTerm.OwnedBy(actor.Principal)]);

        return new AccessScope.None();
    }
}
