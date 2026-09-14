namespace Access.Domain.Authorization;

/// <summary>Pure join row, composite PK — no surrogate id, nothing joins to this row by id
/// (docs/schema/identity-access-schema.md ROLE_PERMISSIONS).</summary>
public sealed class RolePermission
{
    public long RoleId { get; private set; }
    public long PermissionId { get; private set; }

    private RolePermission() { }

    public static RolePermission Create(long roleId, long permissionId) =>
        new() { RoleId = roleId, PermissionId = permissionId };
}
