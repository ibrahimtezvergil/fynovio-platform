namespace CRM.Application;

/// <summary>The action-key vocabulary CRM registers. Handlers keep their own private literals (the
/// authorization call site stays greppable); this class exists so the catalog and the module capability
/// template cannot drift apart — a test proves every catalog key appears in the template.</summary>
public static class CrmActionKeys
{
    public const string OpportunityCreate = "crm.opportunity.create";
    public const string OpportunityAddLine = "crm.opportunity.add_line";
    public const string OpportunityCancelLine = "crm.opportunity.cancel_line";
    public const string OpportunityOpen = "crm.opportunity.open";
    public const string OpportunityChangeStage = "crm.opportunity.change_stage";
    public const string OpportunityWin = "crm.opportunity.win";
    public const string OpportunityLose = "crm.opportunity.lose";
    public const string OpportunityReassign = "crm.opportunity.reassign";
    public const string OpportunityArchive = "crm.opportunity.archive";
    public const string OpportunityRestore = "crm.opportunity.restore";
    public const string OpportunityRead = "crm.opportunity.read";
    public const string OpportunityList = "crm.opportunity.list";
    public const string PartyReferenceSearch = "crm.reference.party.search";
    public const string PartyReferenceCreate = "crm.reference.party.create";
    public const string SettingsRead = "crm.settings.read";
    public const string SettingsUpdate = "crm.settings.update";
    public const string AssignmentManage = "crm.opportunity.assignment.manage";
    public const string OpportunityUpdateCustomFields = "crm.opportunity.update_custom_fields";
}
