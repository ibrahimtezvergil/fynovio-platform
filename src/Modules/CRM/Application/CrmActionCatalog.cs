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
        new("crm.opportunity.create", "Opportunity"),
        new("crm.opportunity.add_line", "Opportunity"),
        new("crm.opportunity.cancel_line", "Opportunity"),
        new("crm.opportunity.open", "Opportunity"),
        new("crm.opportunity.change_stage", "Opportunity"),
        new("crm.opportunity.win", "Opportunity", RiskClass: "high"),
        new("crm.opportunity.lose", "Opportunity", RiskClass: "high"),
        new("crm.opportunity.reassign", "Opportunity", RiskClass: "high"),
        new("crm.opportunity.read", "Opportunity"),
        new("crm.opportunity.list", "Opportunity")
    ];
}
