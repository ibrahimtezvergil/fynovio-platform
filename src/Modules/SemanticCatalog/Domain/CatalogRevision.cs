using Contracts;

namespace SemanticCatalog.Domain;

/// <summary>The tenant's catalog revision: one row per tenant, bumped by every published change set. It is also the lock that
/// serializes publishing for a tenant (`SELECT … FOR UPDATE`), which is what makes the stale-base check race-free.</summary>
public sealed class CatalogRevision
{
    public TenantId TenantId { get; private set; }
    public long Revision { get; private set; }

    private CatalogRevision() { }
}
