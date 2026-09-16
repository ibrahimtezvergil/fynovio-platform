using MasterData.Domain;
using MasterData.Evidence;
using MasterData.Idempotency;
using MasterData.Outbox;
using Microsoft.EntityFrameworkCore;

namespace MasterData.Persistence;

/// <summary>One PostgreSQL schema per module (doc 07 §3) — this module owns the
/// `masterdata` schema exclusively. Never referenced directly by another module's
/// project; cross-module access goes through PartyRef/IPartyDirectory/
/// IPartyIdentityResolver (Contracts) and the outbox, not this DbContext.</summary>
public sealed class MasterDataDbContext : DbContext
{
    public const string Schema = "masterdata";

    public MasterDataDbContext(DbContextOptions<MasterDataDbContext> options) : base(options)
    {
    }

    public DbSet<Party> Parties => Set<Party>();
    public DbSet<PartyRelationship> PartyRelationships => Set<PartyRelationship>();
    public DbSet<PartyExternalIdentity> PartyExternalIdentities => Set<PartyExternalIdentity>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<EvidenceRecord> EvidenceRecords => Set<EvidenceRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MasterDataDbContext).Assembly);
    }
}
