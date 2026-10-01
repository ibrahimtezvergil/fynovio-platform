using System.Text.RegularExpressions;
using Contracts;

namespace SemanticCatalog.Domain;

/// <summary>A tenant field definition owned by the Semantic Catalog (OD-5; adr-semantic-catalog-changeset.md S-1). It
/// only describes shape: values live on the owning aggregate (OD-6). `Key` is the immutable key a stored value is filed
/// under; `Label` is what people see and may change. Behavior is carried over unchanged from the tier-1 definition in
/// CRM (adr-tier1-custom-fields.md).</summary>
public sealed partial class CatalogFieldDefinition
{
    public const int MaxLabelLength = 100;
    public const int MaxSortOrder = 10_000;

    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string OwnerContext { get; private set; } = null!;
    public string ObjectType { get; private set; } = null!;
    public string Key { get; private set; } = null!;
    public string Label { get; private set; } = null!;
    public FieldType Type { get; private set; }
    public bool IsRequired { get; private set; }
    public string ConfigJson { get; private set; } = "{}";
    public FieldStatus Status { get; private set; }
    public int SortOrder { get; private set; }
    public long RowVersion { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public FieldConfig Config => FieldConfigRules.FromJson(ConfigJson);
    public bool IsActive => Status == FieldStatus.Active;

    private CatalogFieldDefinition() { }

    public static CatalogFieldDefinition Create(
        TenantId tenantId,
        string ownerContext,
        string objectType,
        string key,
        string label,
        FieldType type,
        bool isRequired = false,
        FieldConfig? config = null,
        int sortOrder = 0)
    {
        if (!CatalogOwners.IsKnown(ownerContext, objectType))
            throw new ArgumentException($"Unknown definition owner '{ownerContext}/{objectType}'.", nameof(objectType));
        if (!Enum.IsDefined(type))
            throw new ArgumentException("Unknown field type.", nameof(type));
        if (key is null || !KeyFormat().IsMatch(key))
            throw new ArgumentException("Field key must start with a lowercase letter and contain 2–63 lowercase letters, digits or underscores.", nameof(key));

        var now = DateTimeOffset.UtcNow;
        return new CatalogFieldDefinition
        {
            TenantId = tenantId,
            OwnerContext = ownerContext,
            ObjectType = objectType,
            Key = key,
            Label = RequiredLabel(label),
            Type = type,
            IsRequired = isRequired,
            ConfigJson = (config ?? FieldConfig.Empty).Validate(type).ToJson(),
            Status = FieldStatus.Active,
            SortOrder = ValidSortOrder(sortOrder),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Key, type and decimal scale are fixed at creation; a change there is deprecate + new field.</summary>
    public void Update(string label, bool isRequired, FieldConfig config, int sortOrder)
    {
        var normalized = config.Validate(Type);
        Config.EnsureCompatibleReplacement(normalized);

        Label = RequiredLabel(label);
        IsRequired = isRequired;
        ConfigJson = normalized.ToJson();
        SortOrder = ValidSortOrder(sortOrder);
        Touch();
    }

    public void Deprecate()
    {
        if (Status == FieldStatus.Deprecated)
            throw new InvalidOperationException("The field is already deprecated.");
        Status = FieldStatus.Deprecated;
        Touch();
    }

    /// <summary>Possible because the key never disappears: values written before deprecation become editable again.</summary>
    public void Reactivate()
    {
        if (Status == FieldStatus.Active)
            throw new InvalidOperationException("The field is already active.");
        Status = FieldStatus.Active;
        Touch();
    }

    public FieldDefinition ToReadModel() =>
        new(Id, TenantId, OwnerContext, ObjectType, Key, Label, Type, IsRequired, Config, Status, SortOrder, RowVersion);

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
