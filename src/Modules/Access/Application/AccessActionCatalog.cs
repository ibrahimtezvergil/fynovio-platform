namespace Access.Application;

public sealed record ActionRegistryDescriptor(string ActionKey, string OwnerModule, string ResourceType, string? RiskClass = null);

/// <summary>The code-manifest source of truth for the action registry (round 1
/// decision #5). Phase 1.5 registers only Access's own vocabulary — `crm.opportunity.*`
/// is NOT seeded here (gap-closure §3; CRM isn't touched by this plan). Phase 2
/// registers its own action keys when it adds enforcement.
/// `AccessActionCatalogSeeder` projects this into `access.actions` idempotently at
/// Host startup.</summary>
public static class AccessActionCatalog
{
    public static readonly IReadOnlyList<ActionRegistryDescriptor> All =
    [
        new("access.role_assignment.grant", "Access", "Access.RoleAssignment", RiskClass: "high"),
        new("access.role_assignment.revoke", "Access", "Access.RoleAssignment", RiskClass: "high"),
        new("access.role.manage", "Access", "Access.Role"),
        new("access.permission_set.manage", "Access", "Access.PermissionSet"),
        new("identity.membership.invite", "Access", "Identity.TenantMembership", RiskClass: "high")
    ];
}
