using Collaboration.Domain;
using Collaboration.Idempotency;
using Collaboration.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Collaboration.Persistence;

public sealed class CollaborationDbContext(DbContextOptions<CollaborationDbContext> options) : DbContext(options)
{
    public const string Schema = "collaboration";

    public DbSet<CalendarEntry> CalendarEntries => Set<CalendarEntry>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CollaborationDbContext).Assembly);
    }
}
