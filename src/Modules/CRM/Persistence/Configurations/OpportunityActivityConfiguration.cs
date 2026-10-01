using Contracts;
using CRM.Activity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Persistence.Configurations;

public sealed class OpportunityActivityConfiguration : IEntityTypeConfiguration<OpportunityActivityEntry>
{
    public void Configure(EntityTypeBuilder<OpportunityActivityEntry> builder)
    {
        builder.ToTable("opportunity_activity");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TenantId).HasConversion(id => id.Value, value => new TenantId(value)).IsRequired();
        builder.Property(e => e.Kind).HasMaxLength(40);
        builder.Property(e => e.Payload).HasColumnType("jsonb").IsRequired();

        // No foreign key to opportunities: a projection is rebuilt from the outbox and never constrains its source.
        builder.HasIndex(e => new { e.TenantId, e.EventId }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.OpportunityId, e.OccurredAt });
    }
}

public sealed class ConsumedEventConfiguration : IEntityTypeConfiguration<ConsumedEvent>
{
    public void Configure(EntityTypeBuilder<ConsumedEvent> builder)
    {
        builder.ToTable("consumed_events");
        builder.HasKey(e => new { e.Consumer, e.EventId });
        builder.Property(e => e.Consumer).HasMaxLength(100);
        builder.Property(e => e.TenantId).HasConversion(id => id.Value, value => new TenantId(value)).IsRequired();
    }
}
