namespace Contracts;

/// <summary>Marker for aggregates using an explicit bigint row_version concurrency token
/// (see docs/schema/crm-sales-schema.md revision 2, item 4, and
/// docs/schema/identity-access-schema.md revision 2 — chosen over Postgres xmin to keep one
/// version representation consistent with <see cref="EntityVersion"/>). Pure marker with no
/// ORM dependency, so every module may reference it under the "Contracts only" rule (doc 08).
/// Whatever increments the version is NOT here — Contracts may not depend on EF Core ("no ORM",
/// doc 08's Contracts row). CRM's <c>Opportunity</c> increments its own version inside its domain
/// methods, so outbox/evidence rows written in the same transaction see the new value; Access
/// uses its own <c>Access.Persistence.RowVersionInterceptor</c>. An aggregate must use exactly one
/// of the two mechanisms — registering an interceptor on a context whose aggregates already
/// self-increment would bump the version twice per save.</summary>
public interface IHasRowVersion
{
    long RowVersion { get; }

    void IncrementRowVersion();
}
