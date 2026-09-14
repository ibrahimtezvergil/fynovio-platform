using Contracts;

namespace CRM.Customization;

public enum TenantFieldAggregateType
{
    Party,
    Opportunity
}

public enum TenantFieldValueType
{
    Text,
    Number,
    Boolean,
    Date
}

/// <summary>Tier-1 tenant custom fields (docs/architecture-analysis/15 §7) — metadata
/// governing the custom_fields jsonb column on Party/Opportunity. Not a generic EAV
/// table set: this only describes shape, values live on the aggregate itself.</summary>
public sealed class TenantFieldDefinition
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public TenantFieldAggregateType AggregateType { get; private set; }
    public string FieldName { get; private set; } = null!;
    public TenantFieldValueType FieldType { get; private set; }
    public bool IsRequired { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private TenantFieldDefinition() { }

    public static TenantFieldDefinition Create(
        TenantId tenantId,
        TenantFieldAggregateType aggregateType,
        string fieldName,
        TenantFieldValueType fieldType,
        bool isRequired = false)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException("Field name is required.", nameof(fieldName));

        return new TenantFieldDefinition
        {
            TenantId = tenantId,
            AggregateType = aggregateType,
            FieldName = fieldName,
            FieldType = fieldType,
            IsRequired = isRequired,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
