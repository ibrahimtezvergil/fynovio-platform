namespace CRM.Customization;

/// <summary>`Party` stays in the database CHECK for now, but no definition for it is accepted: Party lives in
/// MasterData, which owns its values (owner decision OD-6, 2026-09-30).</summary>
public enum TenantFieldAggregateType
{
    Party,
    Opportunity
}
