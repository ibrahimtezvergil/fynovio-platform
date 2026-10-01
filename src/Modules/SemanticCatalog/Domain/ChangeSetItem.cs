using System.Text.Json;
using Contracts;

namespace SemanticCatalog.Domain;

/// <summary>One definition change inside a set. The item is **kind-discriminated**: `TargetKind` says whether `PayloadJson` is a
/// <see cref="FieldChangeContent"/> or a <see cref="ViewChangeContent"/>. `TargetId` names the existing definition for
/// update/deprecate/reactivate (null for create).</summary>
public sealed class ChangeSetItem
{
    public const string FieldKind = "field";
    public const string ViewKind = "view";

    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public long ChangeSetId { get; private set; }
    public int Ordinal { get; private set; }
    public string TargetKind { get; private set; } = FieldKind;
    public ChangeOperation Operation { get; private set; }
    public long? TargetId { get; private set; }
    public string PayloadJson { get; private set; } = "{}";

    private ChangeSetItem() { }

    public static ChangeSetItem ForField(ChangeOperation operation, long? targetId, FieldChangeContent content) =>
        Build(FieldKind, operation, targetId, JsonSerializer.Serialize(content, FieldChangeContent.Serializer));

    public static ChangeSetItem ForView(ChangeOperation operation, long? targetId, ViewChangeContent content) =>
        Build(ViewKind, operation, targetId, JsonSerializer.Serialize(content, FieldChangeContent.Serializer));

    private static ChangeSetItem Build(string kind, ChangeOperation operation, long? targetId, string payload)
    {
        if (!Enum.IsDefined(operation))
            throw new ArgumentException("Unknown operation.", nameof(operation));
        if (operation == ChangeOperation.Create ? targetId is not null : targetId is null or <= 0)
            throw new ArgumentException(operation == ChangeOperation.Create ? "A create item has no target id." : "This operation needs the target definition id.", nameof(targetId));

        return new ChangeSetItem { TargetKind = kind, Operation = operation, TargetId = targetId, PayloadJson = payload };
    }

    public FieldChangeContent Field => TargetKind == FieldKind
        ? JsonSerializer.Deserialize<FieldChangeContent>(PayloadJson, FieldChangeContent.Serializer) ?? throw new InvalidOperationException("Change set item payload is empty.")
        : throw new InvalidOperationException($"This is a {TargetKind} item, not a field item.");

    public ViewChangeContent View => TargetKind == ViewKind
        ? JsonSerializer.Deserialize<ViewChangeContent>(PayloadJson, FieldChangeContent.Serializer) ?? throw new InvalidOperationException("Change set item payload is empty.")
        : throw new InvalidOperationException($"This is a {TargetKind} item, not a view item.");

    /// <summary>The definition key the item concerns, whatever its kind — what an event names (keys only, never content).</summary>
    public string Key => TargetKind == FieldKind ? Field.Key : View.Key;

    /// <summary>The identity two items of one set must not share: the same definition cannot be changed twice in one set, because the
    /// second item would be applied against a read that does not see the first.</summary>
    public string TargetIdentity => Operation == ChangeOperation.Create
        ? $"{TargetKind}:new:{(TargetKind == FieldKind ? Field.OwnerContext + "/" + Field.ObjectType : View.OwnerContext + "/" + View.ObjectType)}/{Key}"
        : $"{TargetKind}:id:{TargetId}";

    internal ChangeSetItem Attach(TenantId tenantId, int ordinal)
    {
        TenantId = tenantId;
        Ordinal = ordinal;
        return this;
    }
}

/// <summary>What a field item asks for. Property order is the canonical order the content hash relies on; do not reorder.</summary>
public sealed record FieldChangeContent(
    string OwnerContext,
    string ObjectType,
    string Key,
    string Label,
    FieldType? Type,
    bool IsRequired,
    FieldConfig? Config,
    int SortOrder,
    long ExpectedRowVersion)
{
    /// <summary>The serializer every item payload uses (camelCase, enums as names), so the canonical JSON the content hash covers is stable.</summary>
    public static readonly JsonSerializerOptions Serializer = new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
}

/// <summary>What a view item asks for. A shared table view is an ordered list of columns (adr-semantic-catalog-changeset.md S-7);
/// there is deliberately no sort: the list API cannot sort the full result set, and a page-local order would pass for a complete one.</summary>
public sealed record ViewChangeContent(
    string OwnerContext,
    string ObjectType,
    string Key,
    string Name,
    IReadOnlyList<ViewColumn> Columns,
    int SortOrder,
    long ExpectedRowVersion);

/// <summary>One column of a view: a built-in column of the object's table (`Kind = "builtin"`) or a field, by its immutable key
/// (`Kind = "field"`). Fields are resolved to definition ids when the set is published.</summary>
public sealed record ViewColumn(string Kind, string Key)
{
    public const string BuiltIn = "builtin";
    public const string Field = "field";
}
