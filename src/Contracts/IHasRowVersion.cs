namespace Contracts;

/// <summary>Marker for aggregates using an explicit bigint row_version concurrency token
/// (see docs/schema/crm-sales-schema.md revision 2, item 4, and
/// docs/schema/identity-access-schema.md revision 2 — chosen over Postgres xmin to keep one
/// version representation consistent with <see cref="EntityVersion"/>). Pure marker with no
/// ORM dependency, so every module may reference it under the "Contracts only" rule (doc 08).
/// The actual EF Core <c>SaveChangesInterceptor</c> that applies it is NOT here — Contracts
/// may not depend on EF Core ("no ORM", doc 08's Contracts row) — each module owns its own
/// copy (e.g. <c>CRM.Persistence.RowVersionInterceptor</c>, <c>Access.Persistence.RowVersionInterceptor</c>).</summary>
public interface IHasRowVersion
{
    long RowVersion { get; }

    void IncrementRowVersion();
}
