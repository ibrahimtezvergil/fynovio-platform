using Contracts;

namespace CRM.Tests;

/// <summary>An in-memory <see cref="ILinkTargetDirectory"/>: by default every reference is unavailable (fail-closed, like the real
/// directory for an unknown target); a test passes the answer it needs.</summary>
public sealed class StubLinkTargetDirectory(Func<EntityRef, LinkTargetResolution>? answer = null) : ILinkTargetDirectory
{
    public static StubLinkTargetDirectory None => new();

    /// <summary>Every reference resolves to an accessible record labelled "Party {id}".</summary>
    public static StubLinkTargetDirectory EveryPartyAccessible => new(r => new LinkTargetResolution.Accessible($"Party {r.Id}"));

    public List<IReadOnlyCollection<EntityRef>> Calls { get; } = [];

    public Task<IReadOnlyDictionary<EntityRef, LinkTargetResolution>> ResolveAsync(
        ActorContext actor, IReadOnlyCollection<EntityRef> references, CancellationToken cancellationToken = default)
    {
        Calls.Add(references);
        return Task.FromResult<IReadOnlyDictionary<EntityRef, LinkTargetResolution>>(
            references.Distinct().ToDictionary(r => r, r => answer?.Invoke(r) ?? LinkTargetResolution.Unavailable.Instance));
    }
}
