using Access.Idempotency;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records", AccessDbContext.AccessSchema);
        builder.Property(r => r.TenantId).HasConversion(id => id.Value, v => new TenantId(v)).IsRequired();
        builder.HasKey(r => new { r.TenantId, r.PrincipalIssuer, r.PrincipalSubject, r.Operation, r.IdempotencyKey });
        builder.Property(r => r.ResponsePayload).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(r => r.ExpiresAt);
    }
}
