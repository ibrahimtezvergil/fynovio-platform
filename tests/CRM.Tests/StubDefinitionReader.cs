using Contracts;

namespace CRM.Tests;

/// <summary>An in-memory <see cref="ISemanticDefinitionReader"/>: CRM tests prove how CRM *uses* published definitions;
/// the catalog's own persistence and RLS are proven in SemanticCatalog.Tests.</summary>
public sealed class StubDefinitionReader : ISemanticDefinitionReader
{
    public static StubDefinitionReader None => new();

    private readonly List<FieldDefinition> _definitions = [];

    public StubDefinitionReader(params FieldDefinition[] definitions) => _definitions.AddRange(definitions);

    public FieldDefinition Add(FieldDefinition definition)
    {
        _definitions.Add(definition);
        return definition;
    }

    public void Change(long id, Func<FieldDefinition, FieldDefinition> change)
    {
        var index = _definitions.FindIndex(d => d.Id == id);
        _definitions[index] = change(_definitions[index]);
    }

    public Task<IReadOnlyList<FieldDefinition>> ListFieldsAsync(TenantId tenantId, string ownerContext, string objectType, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FieldDefinition>>(_definitions
            .Where(d => d.TenantId == tenantId && d.OwnerContext == ownerContext && d.ObjectType == objectType)
            .OrderBy(d => d.SortOrder).ThenBy(d => d.Key, StringComparer.Ordinal).ToList());

    public Task<FieldDefinition?> GetFieldAsync(TenantId tenantId, long definitionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_definitions.SingleOrDefault(d => d.TenantId == tenantId && d.Id == definitionId));
}

/// <summary>Builds the read model the way a published definition looks to CRM (owner `crm`, object `opportunity`).</summary>
public static class TestFields
{
    private static long _nextId;

    public static FieldDefinition Create(
        TenantId tenant, string key, string label, FieldType type, bool isRequired = false, FieldConfig? config = null, int sortOrder = 0) =>
        new(Interlocked.Increment(ref _nextId), tenant, "crm", "opportunity", key, label, type, isRequired, config ?? FieldConfig.Empty,
            FieldStatus.Active, sortOrder, 1);
}
