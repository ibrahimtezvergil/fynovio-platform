using Contracts;

namespace Collaboration.Tests;

/// <summary>A scriptable ILinkTargetDirectory: Collaboration's tests prove the handlers ask it at the right point and react
/// to each answer; the real resolvers are proven by CRM.Tests and, end to end, by Host.Tests.</summary>
public sealed class StubLinkDirectory(Func<EntityRef, LinkTargetResolution>? answer = null, Exception? failure = null) : ILinkTargetDirectory
{
    private readonly List<IReadOnlyCollection<EntityRef>> _requests = [];

    /// <summary>Every link is accessible, labelled "Target &lt;type&gt; &lt;id&gt;".</summary>
    public static StubLinkDirectory AllowAll() => new(reference => new LinkTargetResolution.Accessible($"Target {reference.EntityType} {reference.Id}", "Subtitle"));

    /// <summary>Every link is unavailable (missing, denied, other tenant, unknown type — indistinguishable).</summary>
    public static StubLinkDirectory DenyAll() => new(_ => LinkTargetResolution.Unavailable.Instance);

    /// <summary>The directory itself blows up.</summary>
    public static StubLinkDirectory Failing() => new(failure: new InvalidOperationException("directory down: SECRET-TARGET-NAME"));

    public int Calls => _requests.Count;

    public IReadOnlyList<IReadOnlyCollection<EntityRef>> Requests => _requests;

    public Task<IReadOnlyDictionary<EntityRef, LinkTargetResolution>> ResolveAsync(
        ActorContext actor, IReadOnlyCollection<EntityRef> references, CancellationToken cancellationToken = default)
    {
        _requests.Add(references.ToList());
        if (failure is not null)
            throw failure;

        IReadOnlyDictionary<EntityRef, LinkTargetResolution> result = references
            .Distinct()
            .ToDictionary(reference => reference, reference => answer!(reference));
        return Task.FromResult(result);
    }
}
