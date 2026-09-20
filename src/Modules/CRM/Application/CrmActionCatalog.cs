namespace CRM.Application;

/// <summary>CRM-local action-descriptor record — deliberately not Access.Application's
/// ActionRegistryDescriptor, since CRM may reference Contracts only (AGENTS.md
/// Architecture Rules). Host maps this into Access's descriptor type at startup,
/// because Host — and only Host — is allowed to know about both modules
/// (architecture plan §14: "Action Registry = Platform + owning business domain
/// vocabulary").</summary>
public sealed record CrmActionDescriptor(string ActionKey, string ResourceType, string? RiskClass = null);

public static class CrmActionCatalog
{
    public static readonly IReadOnlyList<CrmActionDescriptor> All =
    [
        new(CrmActionKeys.OpportunityCreate, "Opportunity"),
        new(CrmActionKeys.OpportunityAddLine, "Opportunity"),
        new(CrmActionKeys.OpportunityCancelLine, "Opportunity"),
        new(CrmActionKeys.OpportunityOpen, "Opportunity"),
        new(CrmActionKeys.OpportunityChangeStage, "Opportunity"),
        new(CrmActionKeys.OpportunityWin, "Opportunity", RiskClass: "high"),
        new(CrmActionKeys.OpportunityLose, "Opportunity", RiskClass: "high"),
        new(CrmActionKeys.OpportunityReassign, "Opportunity", RiskClass: "high"),
        new(CrmActionKeys.OpportunityRead, "Opportunity"),
        new(CrmActionKeys.OpportunityList, "Opportunity"),
        new(CrmActionKeys.PartyReferenceSearch, "PartyReference")
    ];
}
