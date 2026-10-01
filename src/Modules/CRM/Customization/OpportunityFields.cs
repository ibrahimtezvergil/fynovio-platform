namespace CRM.Customization;

/// <summary>How CRM addresses its opportunity field definitions in the Semantic Catalog (OD-5): CRM keeps the values on
/// the opportunity, the catalog owns the definitions.</summary>
public static class OpportunityFields
{
    public const string OwnerContext = "crm";
    public const string ObjectType = "opportunity";
}
