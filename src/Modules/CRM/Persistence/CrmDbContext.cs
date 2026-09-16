using CRM.Customization;
using CRM.Domain;
using CRM.Evidence;
using CRM.Idempotency;
using CRM.Outbox;
using Microsoft.EntityFrameworkCore;

namespace CRM.Persistence;

/// <summary>One PostgreSQL schema per module (doc 07 §3) — this module owns the `crm`
/// schema exclusively. Never referenced directly by another module's project; cross-module
/// access goes through EntityRef-shaped values (see Contracts) and the outbox, not this
/// DbContext (doc 08).</summary>
public sealed class CrmDbContext : DbContext
{
    public const string Schema = "crm";

    public CrmDbContext(DbContextOptions<CrmDbContext> options) : base(options)
    {
    }

    public DbSet<CustomerNeed> CustomerNeeds => Set<CustomerNeed>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<OpportunityLine> OpportunityLines => Set<OpportunityLine>();
    public DbSet<OpportunityNeed> OpportunityNeeds => Set<OpportunityNeed>();
    public DbSet<TenantFieldDefinition> TenantFieldDefinitions => Set<TenantFieldDefinition>();
    public DbSet<PipelineDefinition> PipelineDefinitions => Set<PipelineDefinition>();
    public DbSet<PipelineDefinitionVersion> PipelineDefinitionVersions => Set<PipelineDefinitionVersion>();
    public DbSet<PipelineStage> PipelineStages => Set<PipelineStage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<EvidenceRecord> EvidenceRecords => Set<EvidenceRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
    }
}
