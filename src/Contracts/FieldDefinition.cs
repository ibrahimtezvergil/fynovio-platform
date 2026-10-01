namespace Contracts;

/// <summary>A published field definition as consumers see it. Identity is `(tenant, owner context, object type, key)`;
/// `Key` is immutable, `Label` is what people read. Owner context and object type are plain lowercase names
/// (`crm` / `opportunity`).</summary>
public sealed record FieldDefinition(
    long Id,
    TenantId TenantId,
    string OwnerContext,
    string ObjectType,
    string Key,
    string Label,
    FieldType Type,
    bool IsRequired,
    FieldConfig Config,
    FieldStatus Status,
    int SortOrder,
    long RowVersion)
{
    public bool IsActive => Status == FieldStatus.Active;
}

/// <summary>A shared table view as consumers see it: an ordered list of columns (adr-semantic-catalog-changeset.md S-7). A column
/// is a built-in one of the object's table (`Kind = "builtin"`) or a field by its immutable key (`Kind = "field"`).</summary>
public sealed record ViewDefinition(
    long Id,
    TenantId TenantId,
    string OwnerContext,
    string ObjectType,
    string Key,
    string Name,
    IReadOnlyList<ViewColumnRef> Columns,
    FieldStatus Status,
    int SortOrder,
    long RowVersion)
{
    public bool IsActive => Status == FieldStatus.Active;
}

public sealed record ViewColumnRef(string Kind, string Key);

/// <summary>A view that depends on a field — what the deprecate dialog lists.</summary>
public sealed record DependentView(long Id, string Key, string Name);
