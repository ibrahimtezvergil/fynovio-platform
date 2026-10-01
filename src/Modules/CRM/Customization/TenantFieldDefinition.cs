using System.Text.RegularExpressions;
using Contracts;

namespace CRM.Customization;

/// <summary>Tier-1 tenant custom fields (doc 15 §3; adr-tier1-custom-fields.md) — metadata governing the
/// `custom_fields` jsonb column of the owning aggregate. Not a generic EAV table set: this only describes
/// shape, values live on the aggregate itself. `FieldName` is the immutable key a stored value is filed
/// under; `Label` is what people see and may change.</summary>
public sealed partial class TenantFieldDefinition
{
    public const int MaxLabelLength = 100;
    public const int MaxSortOrder = 10_000;

    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public TenantFieldAggregateType AggregateType { get; private set; }
    public string FieldName { get; private set; } = null!;
    public string Label { get; private set; } = null!;
    public TenantFieldValueType FieldType { get; private set; }
    public bool IsRequired { get; private set; }
    public string ConfigJson { get; private set; } = "{}";
    public TenantFieldStatus Status { get; private set; }
    public int SortOrder { get; private set; }
    public TenantFieldOwnerScope OwnerScope { get; private set; }
    public long RowVersion { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public TenantFieldConfig Config => TenantFieldConfig.FromJson(ConfigJson);
    public bool IsActive => Status == TenantFieldStatus.Active;

    private TenantFieldDefinition() { }

    public static TenantFieldDefinition Create(
        TenantId tenantId,
        TenantFieldAggregateType aggregateType,
        string fieldName,
        string label,
        TenantFieldValueType fieldType,
        bool isRequired = false,
        TenantFieldConfig? config = null,
        int sortOrder = 0)
    {
        if (!Enum.IsDefined(aggregateType))
            throw new ArgumentException("Unknown aggregate type.", nameof(aggregateType));
        if (!Enum.IsDefined(fieldType))
            throw new ArgumentException("Unknown field type.", nameof(fieldType));
        if (fieldName is null || !KeyFormat().IsMatch(fieldName))
            throw new ArgumentException("Field key must start with a lowercase letter and contain 2–63 lowercase letters, digits or underscores.", nameof(fieldName));

        var now = DateTimeOffset.UtcNow;
        return new TenantFieldDefinition
        {
            TenantId = tenantId,
            AggregateType = aggregateType,
            FieldName = fieldName,
            Label = RequiredLabel(label),
            FieldType = fieldType,
            IsRequired = isRequired,
            ConfigJson = (config ?? TenantFieldConfig.Empty).Validate(fieldType).ToJson(),
            Status = TenantFieldStatus.Active,
            SortOrder = ValidSortOrder(sortOrder),
            OwnerScope = TenantFieldOwnerScope.Tenant,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Key, type and decimal scale are fixed at creation; a change there is deprecate + new field.</summary>
    public void Update(string label, bool isRequired, TenantFieldConfig config, int sortOrder)
    {
        var normalized = config.Validate(FieldType);
        Config.EnsureCompatibleReplacement(normalized);

        Label = RequiredLabel(label);
        IsRequired = isRequired;
        ConfigJson = normalized.ToJson();
        SortOrder = ValidSortOrder(sortOrder);
        Touch();
    }

    public void Deprecate()
    {
        if (Status == TenantFieldStatus.Deprecated)
            throw new InvalidOperationException("The field is already deprecated.");
        Status = TenantFieldStatus.Deprecated;
        Touch();
    }

    /// <summary>Possible because the key never disappears: values written before deprecation become editable again.</summary>
    public void Reactivate()
    {
        if (Status == TenantFieldStatus.Active)
            throw new InvalidOperationException("The field is already active.");
        Status = TenantFieldStatus.Active;
        Touch();
    }

    private void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        RowVersion++;
    }

    private static string RequiredLabel(string label) =>
        string.IsNullOrWhiteSpace(label) || label.Trim().Length > MaxLabelLength
            ? throw new ArgumentException($"A label of 1–{MaxLabelLength} characters is required.", nameof(label))
            : label.Trim();

    private static int ValidSortOrder(int sortOrder) =>
        sortOrder is < 0 or > MaxSortOrder
            ? throw new ArgumentOutOfRangeException(nameof(sortOrder), $"Sort order must be between 0 and {MaxSortOrder}.")
            : sortOrder;

    [GeneratedRegex(@"^[a-z][a-z0-9_]{1,62}$")]
    private static partial Regex KeyFormat();
}
