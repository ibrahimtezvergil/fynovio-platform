using Contracts;
using Messaging.Domain;
using Microsoft.EntityFrameworkCore;

namespace Messaging.Persistence;

/// <summary>The platform's delivery ledger (schema <c>messaging</c>). Not a business module: it owns no domain facts,
/// only which published fact is owed to which consumer. Migrated after every module, because its RLS migration adds
/// the relay policy to their outbox tables.</summary>
public sealed class MessagingDbContext(DbContextOptions<MessagingDbContext> options) : DbContext(options)
{
    public const string Schema = "messaging";

    public DbSet<EventDelivery> EventDeliveries => Set<EventDelivery>();
    public DbSet<ConsumerRegistration> ConsumerRegistrations => Set<ConsumerRegistration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<EventDelivery>(builder =>
        {
            builder.ToTable("event_deliveries");
            builder.HasKey(d => d.Id);
            builder.Property(d => d.TenantId).HasConversion(id => id.Value, value => new TenantId(value));
            builder.Property(d => d.Consumer).HasMaxLength(100);
            builder.Property(d => d.SourceSchema).HasMaxLength(63);
            builder.Property(d => d.Status).HasConversion(s => DeliveryStatusText.ToDb(s), s => DeliveryStatusText.FromDb(s)).HasMaxLength(20);
            builder.Property(d => d.LastError).HasMaxLength(200);
            builder.Property(d => d.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(d => new { d.Consumer, d.EventId }).IsUnique();
            // The claim scan: due work, oldest first.
            builder.HasIndex(d => new { d.Status, d.NextAttemptAt });
            // The per-aggregate ordering check (E-3.7).
            builder.HasIndex(d => new { d.Consumer, d.TenantId, d.SourceSchema, d.AggregateType, d.AggregateId, d.AggregateVersion, d.SourceId });
            builder.ToTable(t => t.HasCheckConstraint("ck_event_deliveries_status",
                "status IN ('pending', 'processing', 'delivered', 'skipped', 'dead')"));
        });

        modelBuilder.Entity<ConsumerRegistration>(builder =>
        {
            builder.ToTable("consumer_registrations");
            builder.HasKey(r => r.Consumer);
            builder.Property(r => r.Consumer).HasMaxLength(100);
            builder.Property(r => r.StartPolicy).HasConversion<string>().HasMaxLength(20);
            builder.Property(r => r.RegisteredAt).HasDefaultValueSql("now()");
        });
    }
}

/// <summary>The stored spelling of <see cref="DeliveryStatus"/>; raw SQL in the relay and the processor uses the same
/// literals.</summary>
public static class DeliveryStatusText
{
    public const string Pending = "pending";
    public const string Processing = "processing";
    public const string Delivered = "delivered";
    public const string Skipped = "skipped";
    public const string Dead = "dead";

    public static string ToDb(DeliveryStatus status) => status switch
    {
        DeliveryStatus.Pending => Pending,
        DeliveryStatus.Processing => Processing,
        DeliveryStatus.Delivered => Delivered,
        DeliveryStatus.Skipped => Skipped,
        DeliveryStatus.Dead => Dead,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static DeliveryStatus FromDb(string value) => value switch
    {
        Pending => DeliveryStatus.Pending,
        Processing => DeliveryStatus.Processing,
        Delivered => DeliveryStatus.Delivered,
        Skipped => DeliveryStatus.Skipped,
        Dead => DeliveryStatus.Dead,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };
}
