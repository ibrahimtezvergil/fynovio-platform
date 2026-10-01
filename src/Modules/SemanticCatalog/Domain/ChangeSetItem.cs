using System.Text.Json;
using Contracts;

namespace SemanticCatalog.Domain;

/// <summary>One definition change inside a set. `PayloadJson` is the canonical <see cref="FieldChangeContent"/>; `TargetId`
/// names the existing definition for update/deprecate/reactivate (null for create).</summary>
public sealed class ChangeSetItem
{
    public const string FieldKind = "field";

    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public long ChangeSetId { get; private set; }
    public int Ordinal { get; private set; }
    public string TargetKind { get; private set; } = FieldKind;
    public FieldOperation Operation { get; private set; }
    public long? TargetId { get; private set; }
    public string PayloadJson { get; private set; } = "{}";

    private ChangeSetItem() { }

    public static ChangeSetItem ForField(FieldOperation operation, long? targetId, FieldChangeContent content)
    {
        if (!Enum.IsDefined(operation))
            throw new ArgumentException("Unknown operation.", nameof(operation));
        if (operation == FieldOperation.Create ? targetId is not null : targetId is null or <= 0)
            throw new ArgumentException(operation == FieldOperation.Create ? "A create item has no target id." : "This operation needs the target definition id.", nameof(targetId));

        return new ChangeSetItem { Operation = operation, TargetId = targetId, PayloadJson = JsonSerializer.Serialize(content, FieldChangeContent.Serializer) };
    }

    public FieldChangeContent Content => JsonSerializer.Deserialize<FieldChangeContent>(PayloadJson, FieldChangeContent.Serializer)
        ?? throw new InvalidOperationException("Change set item payload is empty.");

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
    public static readonly JsonSerializerOptions Serializer = new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
}
