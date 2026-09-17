namespace Contracts;

/// <summary>Critical invariant (round 3 §5, must hold as a contract test — Task 9):
/// `Authorize(actor, action, row) == Allow` IFF `row` is included by
/// `ResolveAccessScope(actor, action, resourceType)`.</summary>
public interface IAccessScopeResolver
{
    Task<AccessScope> ResolveAsync(ActorContext actor, ActionKey action, string resourceType, CancellationToken cancellationToken = default);
}
