using Microsoft.EntityFrameworkCore;
using SemanticCatalog.Domain;
using SemanticCatalog.Evidence;
using SemanticCatalog.Idempotency;
using SemanticCatalog.Outbox;

namespace SemanticCatalog.Persistence;

/// <summary>One PostgreSQL schema per module (doc 07 §3) — this module owns the `semantic` schema exclusively.
/// Never referenced by another module's project; other modules read definitions through `ISemanticDefinitionReader`
/// (Contracts) and learn of changes through the outbox (doc 08).</summary>
public sealed class SemanticCatalogDbContext : DbContext
{
    public const string Schema = "semantic";

    public SemanticCatalogDbContext(DbContextOptions<SemanticCatalogDbContext> options) : base(options)
    {
    }

    public DbSet<CatalogFieldDefinition> FieldDefinitions => Set<CatalogFieldDefinition>();
    public DbSet<CatalogViewDefinition> ViewDefinitions => Set<CatalogViewDefinition>();
    public DbSet<DependencyEdge> DependencyEdges => Set<DependencyEdge>();
    public DbSet<ChangeSet> ChangeSets => Set<ChangeSet>();
    public DbSet<ChangeSetItem> ChangeSetItems => Set<ChangeSetItem>();
    public DbSet<CatalogRevision> CatalogRevisions => Set<CatalogRevision>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<EvidenceRecord> EvidenceRecords => Set<EvidenceRecord>();

    public Task<long> AllocateFieldDefinitionIdAsync(CancellationToken cancellationToken = default) =>
        Database.SqlQueryRaw<long>("SELECT nextval(pg_get_serial_sequence('semantic.field_definitions', 'id')) AS \"Value\"")
            .SingleAsync(cancellationToken);

    public Task<long> AllocateViewDefinitionIdAsync(CancellationToken cancellationToken = default) =>
        Database.SqlQueryRaw<long>("SELECT nextval(pg_get_serial_sequence('semantic.view_definitions', 'id')) AS \"Value\"")
            .SingleAsync(cancellationToken);

    public Task<long> AllocateChangeSetIdAsync(CancellationToken cancellationToken = default) =>
        Database.SqlQueryRaw<long>("SELECT nextval(pg_get_serial_sequence('semantic.change_sets', 'id')) AS \"Value\"")
            .SingleAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SemanticCatalogDbContext).Assembly);
    }
}
