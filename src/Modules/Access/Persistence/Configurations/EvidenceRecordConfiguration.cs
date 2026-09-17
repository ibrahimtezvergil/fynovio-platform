using Access.Evidence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class EvidenceRecordConfiguration : IEntityTypeConfiguration<EvidenceRecord>
{
    public void Configure(EntityTypeBuilder<EvidenceRecord> builder)
    {
        builder.ToTable("evidence_records", AccessDbContext.AccessSchema);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TenantId).HasConversion(id => id.Value, v => new TenantId(v)).IsRequired();
        builder.Property(e => e.Action).IsRequired();
        builder.Property(e => e.Detail).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.AggregateType, e.AggregateId });
        builder.HasIndex(e => e.CorrelationId);
    }
}
