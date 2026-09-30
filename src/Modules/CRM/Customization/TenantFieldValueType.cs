namespace CRM.Customization;

/// <summary>No money type on purpose: money needs one declared rounding point, which belongs to a typed
/// aggregate (adr-tier1-custom-fields.md decision 3).</summary>
public enum TenantFieldValueType
{
    Text,
    LongText,
    Number,
    Decimal,
    Boolean,
    Date,
    Select,
    MultiSelect,
    Email,
    Phone,
    Url
}
