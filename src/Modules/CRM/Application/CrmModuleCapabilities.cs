using Contracts;

namespace CRM.Application;

/// <summary>CRM's system role template (version 1) — what a tenant receives when the module is enabled for it.
/// Explicit action keys only (no `crm.*` wildcard); the platform composes this manifest with every other
/// module's and Access copies it into tenant-local rows. Changing this content later does NOT change tenants
/// already enabled at an earlier version (Phase 1.5 Decision A) — bump <see cref="Version"/> so provenance
/// records which content a tenant received.
/// EXCEPTION, recorded on purpose: `crm.reference.party.create` was added to the v1 write set IN PLACE, not as v2 —
/// enablement is copy-once with no reconciler, so a v2 would leave every already-enabled tenant without it, and
/// nothing is in production yet. From the first production tenant on, a content change MUST bump the version.</summary>
public static class CrmModuleCapabilities
{
    public const string ModuleKey = "crm";
    public const int Version = 1;

    public const string ReadSetKey = "crm_opportunity_read";
    public const string WriteSetKey = "crm_opportunity_write";
    public const string ReassignSetKey = "crm_opportunity_reassign";

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
                new(CrmActionKeys.PartyReferenceSearch),
                new(CrmActionKeys.PartyReferenceCreate)
            ]),
            new PermissionSetTemplate(ReassignSetKey, "CRM — reassign opportunities",
            [
                new(CrmActionKeys.OpportunityReassign)
            ])
        ],
        Roles: []);
}
