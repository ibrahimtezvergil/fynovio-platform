using Contracts;

namespace Access.Domain.Authorization;

/// <summary>The one canonical `TenantAccessRevision` counter (round 3 freeze #17) —
/// increments whenever effective authorization configuration changes (Role,
/// PermissionSet, PermissionSetItem, RoleAssignment). Never conflated with
/// `IHasRowVersion.RowVersion`, which is per-row optimistic concurrency.</summary>
public sealed class TenantAccessState
{
    public TenantId TenantId { get; private set; }
    public long Revision { get; private set; }
    public long RowVersion { get; private set; } = 1;

    private TenantAccessState() { }

    public static TenantAccessState Initialize(TenantId tenantId) =>
        new() { TenantId = tenantId, Revision = 0 };

    public void BumpRevision() => Revision++;
}
