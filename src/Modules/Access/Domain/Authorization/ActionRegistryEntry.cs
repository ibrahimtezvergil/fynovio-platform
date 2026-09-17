namespace Access.Domain.Authorization;

/// <summary>Platform-owned projection of the code-manifest action vocabulary
/// (`Access.Application.AccessActionCatalog.All`) — natural key, no surrogate id
/// (gap-closure §2). Never written by a tenant. A key that disappears from the
/// manifest is marked `IsDeprecated`, never deleted (round 1 decision #5 "rename =
/// deprecate + new key").</summary>
public sealed class ActionRegistryEntry
{
    public string ActionKey { get; private set; } = null!;
    public string OwnerModule { get; private set; } = null!;
    public string ResourceType { get; private set; } = null!;
    public string? RiskClass { get; private set; }
    public bool IsDeprecated { get; private set; }

    private ActionRegistryEntry() { }

    public static ActionRegistryEntry Create(string actionKey, string ownerModule, string resourceType, string? riskClass = null)
    {
        if (string.IsNullOrWhiteSpace(actionKey))
            throw new ArgumentException("Action key is required.", nameof(actionKey));
        if (string.IsNullOrWhiteSpace(ownerModule))
            throw new ArgumentException("Owner module is required.", nameof(ownerModule));
        if (string.IsNullOrWhiteSpace(resourceType))
            throw new ArgumentException("Resource type is required.", nameof(resourceType));

        return new ActionRegistryEntry
        {
            ActionKey = actionKey,
            OwnerModule = ownerModule,
            ResourceType = resourceType,
            RiskClass = riskClass
        };
    }

    public void Deprecate() => IsDeprecated = true;

    /// <summary>A previously-deprecated key reappearing in the manifest (e.g. a revert)
    /// is un-deprecated rather than requiring a brand-new key.</summary>
    public void Reactivate() => IsDeprecated = false;
}
