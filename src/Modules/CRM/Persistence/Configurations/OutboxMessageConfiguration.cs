using Contracts;
using CRM.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();

        builder.HasIndex(m => m.EventId).IsUnique();
        // Partial index — outbox dispatcher scans only undelivered messages.
        builder.HasIndex(m => m.ProcessedAt).HasFilter("processed_at IS NULL");
        builder.HasIndex(m => new { m.TenantId, m.AggregateType, m.AggregateId });
    }
}
