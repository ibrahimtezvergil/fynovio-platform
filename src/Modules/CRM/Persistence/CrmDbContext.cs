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
    public DbSet<CrmSettings> CrmSettings => Set<CrmSettings>();
    public DbSet<OpportunityType> OpportunityTypes => Set<OpportunityType>();
    public DbSet<LostReason> LostReasons => Set<LostReason>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<OpportunityLine> OpportunityLines => Set<OpportunityLine>();
    public DbSet<OpportunityNeed> OpportunityNeeds => Set<OpportunityNeed>();
    public DbSet<TenantFieldDefinition> TenantFieldDefinitions => Set<TenantFieldDefinition>();
    public DbSet<PipelineDefinition> PipelineDefinitions => Set<PipelineDefinition>();
    public DbSet<PipelineDefinitionVersion> PipelineDefinitionVersions => Set<PipelineDefinitionVersion>();
    public DbSet<PipelineStage> PipelineStages => Set<PipelineStage>();
    public DbSet<PipelineStageTransition> PipelineStageTransitions => Set<PipelineStageTransition>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<EvidenceRecord> EvidenceRecords => Set<EvidenceRecord>();

    public Task<long> AllocateConfigurationIdAsync<TEntity>(CancellationToken cancellationToken = default) where TEntity : class
    {
        var query = typeof(TEntity) == typeof(PipelineDefinition)
            ? Database.SqlQueryRaw<long>("SELECT nextval(pg_get_serial_sequence('crm.pipeline_definitions', 'id')) AS \"Value\"")
            : typeof(TEntity) == typeof(PipelineDefinitionVersion)
                ? Database.SqlQueryRaw<long>("SELECT nextval(pg_get_serial_sequence('crm.pipeline_definition_versions', 'id')) AS \"Value\"")
            : typeof(TEntity) == typeof(PipelineStage)
                ? Database.SqlQueryRaw<long>("SELECT nextval(pg_get_serial_sequence('crm.pipeline_stages', 'id')) AS \"Value\"")
            : typeof(TEntity) == typeof(OpportunityType)
                ? Database.SqlQueryRaw<long>("SELECT nextval(pg_get_serial_sequence('crm.opportunity_types', 'id')) AS \"Value\"")
            : typeof(TEntity) == typeof(LostReason)
                ? Database.SqlQueryRaw<long>("SELECT nextval(pg_get_serial_sequence('crm.lost_reasons', 'id')) AS \"Value\"")
            : typeof(TEntity) == typeof(CustomerNeed)
                ? Database.SqlQueryRaw<long>("SELECT nextval(pg_get_serial_sequence('crm.customer_needs', 'id')) AS \"Value\"")
            : throw new ArgumentOutOfRangeException(nameof(TEntity));

        return query.SingleAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
    }
}
