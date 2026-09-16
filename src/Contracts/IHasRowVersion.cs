namespace Contracts;

/// <summary>Marker for aggregates using an explicit bigint row_version concurrency token
/// (see docs/schema/crm-sales-schema.md revision 2, item 4, and
/// docs/schema/identity-access-schema.md revision 2 — chosen over Postgres xmin to keep one
/// version representation consistent with <see cref="EntityVersion"/>). Pure marker with no
/// ORM dependency, so every module may reference it under the "Contracts only" rule (doc 08).
/// CRM's <c>Opportunity</c> increments its own <c>RowVersion</c> inside domain methods for
/// atomic writes with outbox/evidence rows; Access uses a <c>SaveChangesInterceptor</c> instead.</summary>
public interface IHasRowVersion
{
    long RowVersion { get; }

    void IncrementRowVersion();
}
