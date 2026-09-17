using Contracts;

namespace Access.Domain.Authorization;

/// <summary>Pure join row, composite PK — same convention the old `RolePermission`
/// used, now joining `Role` to `PermissionSet` instead of directly to an action
/// (round 1 decision #6: Role -> PermissionSet -> ActionKey). Carries `TenantId` so
/// both FKs can be the tenant-safe composite form (AGENTS.md binding-core #1) and so
/// RLS can apply to this table like every other tenant-scoped one.</summary>
public sealed class RolePermissionSet
{
    public TenantId TenantId { get; private set; }
    public long RoleId { get; private set; }
    public long PermissionSetId { get; private set; }

    private RolePermissionSet() { }

    public static RolePermissionSet Create(TenantId tenantId, long roleId, long permissionSetId) =>
        new() { TenantId = tenantId, RoleId = roleId, PermissionSetId = permissionSetId };
}
