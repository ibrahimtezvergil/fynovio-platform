using Contracts;
using CRM.Evidence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Persistence.Configurations;

public sealed class EvidenceRecordConfiguration : IEntityTypeConfiguration<EvidenceRecord>
{
    public void Configure(EntityTypeBuilder<EvidenceRecord> builder)
    {
        builder.ToTable("evidence_records");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(e => e.Detail).HasColumnType("jsonb").IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.AggregateType, e.AggregateId });
        builder.HasIndex(e => e.CorrelationId);
    }
}
