using Microsoft.EntityFrameworkCore;
using TenantLifecycle.Domain;
using TenantLifecycle.Idempotency;
using TenantLifecycle.Outbox;

namespace TenantLifecycle.Persistence;

public sealed class TenantLifecycleDbContext(DbContextOptions<TenantLifecycleDbContext> options) : DbContext(options)
{
    public const string Schema = "tenant_lifecycle";

    public DbSet<TenantProfile> TenantProfiles => Set<TenantProfile>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenantLifecycleDbContext).Assembly);
    }
}
