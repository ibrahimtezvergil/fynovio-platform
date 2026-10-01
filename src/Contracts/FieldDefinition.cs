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
