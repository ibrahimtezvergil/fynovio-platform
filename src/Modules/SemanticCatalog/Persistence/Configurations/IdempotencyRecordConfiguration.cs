using Contracts;
using SemanticCatalog.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SemanticCatalog.Persistence.Configurations;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records");

        // Natural key — tenant + caller + operation + caller-supplied key. No surrogate id.
        builder.HasKey(r => new { r.TenantId, r.PrincipalIssuer, r.PrincipalSubject, r.Operation, r.IdempotencyKey });

        builder.Property(r => r.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(r => r.ResponsePayload).HasColumnType("jsonb").IsRequired();

        // Bounded retention (doc 04's IdempotencyKey primitive) — a cleanup job purges by this.
        builder.HasIndex(r => r.ExpiresAt);
    }
}
