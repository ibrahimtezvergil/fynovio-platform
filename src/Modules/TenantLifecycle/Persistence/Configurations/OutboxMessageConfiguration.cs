using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TenantLifecycle.Outbox;

namespace TenantLifecycle.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.TenantId).HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(message => message.Payload).HasColumnType("jsonb");
        builder.HasIndex(message => message.EventId).IsUnique();
        builder.HasIndex(message => message.ProcessedAt).HasFilter("processed_at IS NULL");
        builder.HasIndex(message => new { message.TenantId, message.AggregateType, message.AggregateId });
    }
}
