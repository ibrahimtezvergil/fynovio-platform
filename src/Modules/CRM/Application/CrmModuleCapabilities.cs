using Contracts;

namespace CRM.Application;

/// <summary>CRM's system role template (version 1) — what a tenant receives when the module is enabled for it.
/// Explicit action keys only (no `crm.*` wildcard); the platform composes this manifest with every other
/// module's and Access copies it into tenant-local rows. Changing this content later does NOT change tenants
/// already enabled at an earlier version (Phase 1.5 Decision A) — bump <see cref="Version"/> so provenance
/// records which content a tenant received.</summary>
public static class CrmModuleCapabilities
{
    public const string ModuleKey = "crm";
    public const int Version = 1;

    public const string ReadSetKey = "crm_opportunity_read";
    public const string WriteSetKey = "crm_opportunity_write";
    public const string ReassignSetKey = "crm_opportunity_reassign";

    public const string ViewerRoleKey = "crm_viewer";
    public const string SalesRepresentativeRoleKey = "crm_sales_representative";
    public const string ManagerRoleKey = "crm_manager";

    public static readonly ModuleCapabilityManifest Manifest = new(
        ModuleKey,
        "CRM",
        Version,
        PermissionSets:
        [
            new PermissionSetTemplate(ReadSetKey, "CRM — read opportunities",
            [
                new(CrmActionKeys.OpportunityRead),
                new(CrmActionKeys.OpportunityList)
            ]),
            new PermissionSetTemplate(WriteSetKey, "CRM — work opportunities",
            [
                new(CrmActionKeys.OpportunityCreate),
                new(CrmActionKeys.OpportunityAddLine),
                new(CrmActionKeys.OpportunityCancelLine),
                new(CrmActionKeys.OpportunityOpen),
                new(CrmActionKeys.OpportunityChangeStage),
                new(CrmActionKeys.OpportunityWin),
                new(CrmActionKeys.OpportunityLose),
                new(CrmActionKeys.PartyReferenceSearch)
            ]),
            new PermissionSetTemplate(ReassignSetKey, "CRM — reassign opportunities",
            [
                new(CrmActionKeys.OpportunityReassign)
            ])
        ],
        Roles:
        [
            new RoleTemplate(ViewerRoleKey, "CRM Viewer", [ReadSetKey]),
            new RoleTemplate(SalesRepresentativeRoleKey, "CRM Sales Representative", [ReadSetKey, WriteSetKey]),
            new RoleTemplate(ManagerRoleKey, "CRM Manager", [ReadSetKey, WriteSetKey, ReassignSetKey], GrantToTenantAdministrators: true)
        ]);
}
