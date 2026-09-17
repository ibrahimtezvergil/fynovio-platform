using Contracts;

namespace Access.Domain.Authorization;

/// <summary>A single action grant inside a `PermissionSet`, optionally narrowed by a
/// relationship constraint. Phase 1.5's closed set is `null` (unrestricted within
/// scope) or `"owner"` (gap-closure §7) — a new relation value is a new CHECK
/// constraint added when its evaluator exists, not reserved today.</summary>
public sealed class PermissionSetItem
{
    public const string OwnerRelation = "owner";

    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }

    /// <summary>Real property, set by EF's relationship fixup when the item is added
    /// to `PermissionSet.Items` and `SaveChanges()` runs — not passed to `Create()`.
    /// Same pattern as `CRM.Domain.OpportunityLine.OpportunityId`.</summary>
    public long PermissionSetId { get; private set; }

    public string ActionKey { get; private set; } = null!;
    public string? Relation { get; private set; }

    private PermissionSetItem() { }

    internal static PermissionSetItem Create(TenantId tenantId, string actionKey, string? relation)
    {
        if (string.IsNullOrWhiteSpace(actionKey))
            throw new ArgumentException("Action key is required.", nameof(actionKey));
        if (relation is not null && relation != OwnerRelation)
            throw new ArgumentException($"Only '{OwnerRelation}' is a supported relation constraint in Phase 1.5.", nameof(relation));

        return new PermissionSetItem { TenantId = tenantId, ActionKey = actionKey, Relation = relation };
    }
}
