namespace CRM.Persistence;

/// <summary>Marker for aggregates using the explicit bigint row_version concurrency token
/// (see docs/schema/crm-sales-schema.md revision 2, item 4 — chosen over Postgres xmin
/// to keep one version representation consistent with Contracts.EntityVersion).</summary>
public interface IHasRowVersion
{
    long RowVersion { get; }

    void IncrementRowVersion();
}
