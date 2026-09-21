namespace Contracts;

/// <summary>The façade a module that STORES links (Collaboration today) consumes: it resolves any mix of
/// <see cref="EntityRef"/>s through whichever module owns each target, without knowing those modules. See
/// <see cref="ILinkTargetResolver"/> for the privacy contract; this interface adds: an unknown
/// `(BoundedContext, EntityType)`, a reference whose tenant differs from the actor's, and a resolver that fails all
/// resolve to <see cref="LinkTargetResolution.Unavailable"/>.</summary>
public interface ILinkTargetDirectory
{
    /// <summary>The result contains EXACTLY one entry per distinct requested reference — never a missing key.</summary>
    Task<IReadOnlyDictionary<EntityRef, LinkTargetResolution>> ResolveAsync(
        ActorContext actor, IReadOnlyCollection<EntityRef> references, CancellationToken cancellationToken = default);
}
