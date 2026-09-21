using System.Text.RegularExpressions;

namespace Contracts;

/// <summary>Pure composition over the registered <see cref="ILinkTargetResolver"/>s — it knows no module. Resolvers
/// are called one after another (they may share a DbContext per DI scope, which is not safe to use concurrently).
/// A resolver that throws is degraded to `Unavailable` for its references (cancellation still propagates): a broken
/// or slow neighbour must not take the whole calendar down, and failing closed leaks nothing. Hosts that want the
/// fault visible decorate the resolver with logging.</summary>
public sealed partial class LinkTargetDirectory : ILinkTargetDirectory
{
    private readonly Dictionary<(string BoundedContext, string EntityType), ILinkTargetResolver> _resolvers = [];

    public LinkTargetDirectory(IEnumerable<ILinkTargetResolver> resolvers)
    {
        ArgumentNullException.ThrowIfNull(resolvers);
        foreach (var resolver in resolvers)
        {
            if (!IsIdentifier(resolver.BoundedContext) || !IsIdentifier(resolver.EntityType))
                throw new ArgumentException(
                    $"Link resolver '{resolver.GetType().Name}' declares an invalid key '{resolver.BoundedContext}/{resolver.EntityType}'; both parts must match ^[a-z][a-z0-9_]*$.",
                    nameof(resolvers));

            if (!_resolvers.TryAdd((resolver.BoundedContext, resolver.EntityType), resolver))
                throw new ArgumentException(
                    $"More than one link resolver is registered for '{resolver.BoundedContext}/{resolver.EntityType}'.", nameof(resolvers));
        }
    }

    public async Task<IReadOnlyDictionary<EntityRef, LinkTargetResolution>> ResolveAsync(
        ActorContext actor, IReadOnlyCollection<EntityRef> references, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<EntityRef, LinkTargetResolution>();
        var pending = new Dictionary<(string, string), List<EntityRef>>();

        foreach (var reference in references.Distinct())
        {
            if (reference.TenantId != actor.TenantId || !_resolvers.ContainsKey((reference.BoundedContext, reference.EntityType)))
            {
                results[reference] = LinkTargetResolution.Unavailable.Instance;
                continue;
            }

            var key = (reference.BoundedContext, reference.EntityType);
            if (!pending.TryGetValue(key, out var group))
                pending[key] = group = [];
            group.Add(reference);
        }

        foreach (var (key, group) in pending)
        {
            IReadOnlyDictionary<long, LinkTargetResolution>? resolved = null;
            try
            {
                resolved = await _resolvers[key].ResolveAsync(actor, group.Select(r => r.Id).Distinct().ToList(), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Fail closed for this resolver's references only.
            }

            foreach (var reference in group)
                results[reference] = resolved is not null && resolved.TryGetValue(reference.Id, out var resolution)
                    ? resolution
                    : LinkTargetResolution.Unavailable.Instance;
        }

        return results;
    }

    private static bool IsIdentifier(string value) => !string.IsNullOrEmpty(value) && IdentifierPattern().IsMatch(value);

    [GeneratedRegex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();
}
