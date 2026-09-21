namespace Contracts;

/// <summary>Resolves display data for links that point at ONE kind of record — the `(BoundedContext, EntityType)`
/// pair it declares — owned by the implementing module. Implemented by the owning module (or, while a module has no
/// authorization layer of its own, by the module that gates access to it; see the Collaboration ADR), never by the
/// module that stores the link. Same family as <see cref="IPartyDirectory"/> and
/// <see cref="IAuthorizedPrincipalDirectory"/>.
///
/// Privacy contract: the answer is computed for the actor at read time, under the actor's own authorization, and is
/// never cached across actors. A target that does not exist, belongs to another tenant, is denied to the actor, or
/// cannot be resolved must yield <see cref="LinkTargetResolution.Unavailable"/> — never an exception, never a
/// different shape. Batch-only: a list of N links costs one round-trip per resolver, not N.</summary>
public interface ILinkTargetResolver
{
    /// <summary>Lower-case identifier (`^[a-z][a-z0-9_]*$`), the same grammar the link columns are constrained to.</summary>
    string BoundedContext { get; }

    /// <summary>Lower-case identifier (`^[a-z][a-z0-9_]*$`).</summary>
    string EntityType { get; }

    /// <summary>One result per DISTINCT requested id; an id missing from the result is treated by callers as
    /// <see cref="LinkTargetResolution.Unavailable"/>. Ids are already scoped to `actor.TenantId` by the caller.</summary>
    Task<IReadOnlyDictionary<long, LinkTargetResolution>> ResolveAsync(
        ActorContext actor, IReadOnlyCollection<long> ids, CancellationToken cancellationToken = default);
}
