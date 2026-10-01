namespace Contracts;

/// <summary>Read side of the Semantic Catalog (OD-5; adr-semantic-catalog-changeset.md S-1). Consumers read published
/// definitions through this and never see the catalog's persistence. Each call opens its own tenant-scoped
/// transaction, so it is safe to call from inside another module's transaction.</summary>
public interface ISemanticDefinitionReader
{
    /// <summary>Every field of one object type, active and deprecated, ordered by sort order then key.</summary>
    Task<IReadOnlyList<FieldDefinition>> ListFieldsAsync(TenantId tenantId, string ownerContext, string objectType, CancellationToken cancellationToken = default);

    Task<FieldDefinition?> GetFieldAsync(TenantId tenantId, long definitionId, CancellationToken cancellationToken = default);

    /// <summary>The active views whose columns include this field (the dependency edges computed when a view is published).
    /// Deprecating a field is allowed regardless; this is what the person is shown first.</summary>
    Task<IReadOnlyList<DependentView>> ListDependentViewsAsync(TenantId tenantId, long fieldDefinitionId, CancellationToken cancellationToken = default);
}
