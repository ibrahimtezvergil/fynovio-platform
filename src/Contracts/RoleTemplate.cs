namespace Contracts;

/// <summary>A security persona a module ships, composed of the manifest's permission-set templates by key.
/// `GrantToTenantAdministrators` assigns the copied role to the tenant's administrators that exist
/// when the module is enabled — never to anyone who becomes an administrator later.</summary>
public sealed record RoleTemplate(
    string Key,
    string Name,
    IReadOnlyList<string> PermissionSetKeys,
    bool GrantToTenantAdministrators = false);
