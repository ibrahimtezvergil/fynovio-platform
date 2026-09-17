using Access.Outbox;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages", AccessDbContext.AccessSchema);
        builder.HasKey(m => m.Id);
        builder.Property(m => m.TenantId).HasConversion(id => id.Value, v => new TenantId(v)).IsRequired();
        builder.Property(m => m.EventType).IsRequired();
        builder.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(m => m.EventId).IsUnique();
        builder.HasIndex(m => new { m.TenantId, m.ProcessedAt });
    }
}
