namespace Access.Domain.Authorization;

/// <summary>Pure join row, composite PK — same convention the old `RolePermission`
/// used, now joining `Role` to `PermissionSet` instead of directly to an action
/// (round 1 decision #6: Role -> PermissionSet -> ActionKey).</summary>
public sealed class RolePermissionSet
{
    public long RoleId { get; private set; }
    public long PermissionSetId { get; private set; }

    private RolePermissionSet() { }

    public static RolePermissionSet Create(long roleId, long permissionSetId) =>
        new() { RoleId = roleId, PermissionSetId = permissionSetId };
}
