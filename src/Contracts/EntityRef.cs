namespace Contracts;

/// <summary>Typed domain object identity, owned by its bounded context. Use this — never a raw
/// foreign key — to reference an entity owned by a different module's schema
/// (see 07 Target Reference Architecture §3: no cross-schema foreign keys).
/// Immutable reference; the referenced object may evolve through versions (see <see cref="EntityVersion"/>).</summary>
public readonly record struct EntityRef
{
    public TenantId TenantId { get; }
    public string BoundedContext { get; }
    public string EntityType { get; }
    public long Id { get; }

    public EntityRef(TenantId tenantId, string boundedContext, string entityType, long id)
    {
        if (string.IsNullOrWhiteSpace(boundedContext))
            throw new ArgumentException("BoundedContext is required.", nameof(boundedContext));
        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("EntityType is required.", nameof(entityType));
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), "Id must be a positive identifier.");

        TenantId = tenantId;
        BoundedContext = boundedContext;
        EntityType = entityType;
        Id = id;
    }

    public override string ToString() => $"{BoundedContext}/{EntityType}/{Id}@{TenantId}";
}
