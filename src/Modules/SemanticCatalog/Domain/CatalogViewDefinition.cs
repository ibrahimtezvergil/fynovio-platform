using System.Text.Json;
using System.Text.RegularExpressions;
using Contracts;

namespace SemanticCatalog.Domain;

/// <summary>A tenant-shared table view (adr-semantic-catalog-changeset.md S-7), created and changed only through a change set.
/// It stores *which* columns in *what* order — never data. Field columns are validated against the definitions when the set is
/// published (they must exist, and be active for a create or update); that needs the database, so it lives in the publisher.</summary>
public sealed partial class CatalogViewDefinition
{
    public const int MaxNameLength = 100;
    public const int MaxColumns = 20;
    public const int MaxSortOrder = 10_000;
    public const string TableKind = "table";

    private static readonly JsonSerializerOptions Serializer = ViewJson.Serializer;

    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string OwnerContext { get; private set; } = null!;
    public string ObjectType { get; private set; } = null!;
    public string Key { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Kind { get; private set; } = TableKind;
    public string ColumnsJson { get; private set; } = "[]";
    public FieldStatus Status { get; private set; }
    public int SortOrder { get; private set; }
    public long RowVersion { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<ViewColumn> Columns => JsonSerializer.Deserialize<List<ViewColumn>>(ColumnsJson, Serializer) ?? [];
    public bool IsActive => Status == FieldStatus.Active;

    private CatalogViewDefinition() { }

    public static CatalogViewDefinition Create(
        TenantId tenantId, string ownerContext, string objectType, string key, string name, IReadOnlyList<ViewColumn> columns, int sortOrder = 0)
    {
        if (!CatalogOwners.IsKnown(ownerContext, objectType))
            throw new ArgumentException($"Unknown definition owner '{ownerContext}/{objectType}'.", nameof(objectType));
        if (key is null || !KeyFormat().IsMatch(key))
            throw new ArgumentException("View key must start with a lowercase letter and contain 2–63 lowercase letters, digits or underscores.", nameof(key));

        var now = DateTimeOffset.UtcNow;
        return new CatalogViewDefinition
        {
            TenantId = tenantId,
            OwnerContext = ownerContext,
            ObjectType = objectType,
            Key = key,
            Name = RequiredName(name),
            ColumnsJson = Serialize(ValidColumns(ownerContext, objectType, columns)),
            Status = FieldStatus.Active,
            SortOrder = ValidSortOrder(sortOrder),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>The key is fixed at creation (a change is deprecate + new view), like a field's.</summary>
    public void Update(string name, IReadOnlyList<ViewColumn> columns, int sortOrder)
    {
        Name = RequiredName(name);
        ColumnsJson = Serialize(ValidColumns(OwnerContext, ObjectType, columns));
        SortOrder = ValidSortOrder(sortOrder);
        Touch();
    }

    public void Deprecate()
    {
        if (Status == FieldStatus.Deprecated)
            throw new InvalidOperationException("The view is already deprecated.");
        Status = FieldStatus.Deprecated;
        Touch();
    }

    public void Reactivate()
    {
        if (Status == FieldStatus.Active)
            throw new InvalidOperationException("The view is already active.");
        Status = FieldStatus.Active;
        Touch();
    }

    public ViewDefinition ToReadModel() =>
        new(Id, TenantId, OwnerContext, ObjectType, Key, Name, Columns.Select(c => new ViewColumnRef(c.Kind, c.Key)).ToList(), Status, SortOrder, RowVersion);

    private void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        RowVersion++;
    }

    private static string Serialize(IReadOnlyList<ViewColumn> columns) => JsonSerializer.Serialize(columns, Serializer);

    /// <summary>1–20 columns, no column twice, built-in ones from the object's allow-list, field ones by a well-formed key.</summary>
    private static IReadOnlyList<ViewColumn> ValidColumns(string ownerContext, string objectType, IReadOnlyList<ViewColumn> columns)
    {
        if (columns is not { Count: > 0 })
            throw new ArgumentException("A view needs at least one column.", nameof(columns));
        if (columns.Count > MaxColumns)
            throw new ArgumentException($"A view can have at most {MaxColumns} columns.", nameof(columns));

        var builtIn = CatalogOwners.BuiltInViewColumns(ownerContext, objectType);
        foreach (var column in columns)
        {
            switch (column.Kind)
            {
                case ViewColumn.BuiltIn when !builtIn.Contains(column.Key):
                    throw new ArgumentException($"'{column.Key}' is not a built-in column of this view.", nameof(columns));
                case ViewColumn.Field when column.Key is null || !KeyFormat().IsMatch(column.Key):
                    throw new ArgumentException($"'{column.Key}' is not a valid field key.", nameof(columns));
                case ViewColumn.BuiltIn or ViewColumn.Field:
                    break;
                default:
                    throw new ArgumentException($"Unknown column kind '{column.Kind}'.", nameof(columns));
            }
        }

        if (columns.Select(c => (c.Kind, c.Key)).Distinct().Count() != columns.Count)
            throw new ArgumentException("A column cannot appear twice in a view.", nameof(columns));
        return columns;
    }

    private static string RequiredName(string name) =>
        string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength
            ? throw new ArgumentException($"A name of 1–{MaxNameLength} characters is required.", nameof(name))
            : name.Trim();

    private static int ValidSortOrder(int sortOrder) =>
        sortOrder is < 0 or > MaxSortOrder
            ? throw new ArgumentOutOfRangeException(nameof(sortOrder), $"Sort order must be between 0 and {MaxSortOrder}.")
            : sortOrder;

    [GeneratedRegex(@"^[a-z][a-z0-9_]{1,62}$")]
    private static partial Regex KeyFormat();
}

internal static class ViewJson
{
    public static readonly JsonSerializerOptions Serializer = new(JsonSerializerDefaults.Web);
}

/// <summary>`from` → `to`: this view uses that field. Computed when the view is published, in the same transaction, and never
/// edited by hand (adr-semantic-catalog-changeset.md S-7). Keyed by the definition **id**, so a key can never dangle.</summary>
public sealed class DependencyEdge
{
    public const string ViewKind = "view";
    public const string FieldKind = "field";

    public TenantId TenantId { get; private set; }
    public string FromKind { get; private set; } = ViewKind;
    public long FromId { get; private set; }
    public string ToKind { get; private set; } = FieldKind;
    public long ToId { get; private set; }

    private DependencyEdge() { }

    public static DependencyEdge ViewUsesField(TenantId tenantId, long viewId, long fieldId) =>
        new() { TenantId = tenantId, FromId = viewId, ToId = fieldId };
}
