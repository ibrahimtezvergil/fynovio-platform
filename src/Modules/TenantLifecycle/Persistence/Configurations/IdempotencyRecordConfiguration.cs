using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TenantLifecycle.Idempotency;

namespace TenantLifecycle.Persistence.Configurations;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records");
        builder.HasKey(record => new { record.TenantId, record.PrincipalIssuer, record.PrincipalSubject, record.Operation, record.IdempotencyKey });
        builder.Property(record => record.TenantId).HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(record => record.ResponsePayload).HasColumnType("jsonb");
        builder.HasIndex(record => record.ExpiresAt);
    }
}
