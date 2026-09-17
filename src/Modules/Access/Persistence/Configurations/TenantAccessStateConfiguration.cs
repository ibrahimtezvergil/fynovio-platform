using Access.Domain.Authorization;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class TenantAccessStateConfiguration : IEntityTypeConfiguration<TenantAccessState>
{
    public void Configure(EntityTypeBuilder<TenantAccessState> builder)
    {
        builder.ToTable("tenant_access_state", AccessDbContext.AccessSchema);

        // tenant_id is the natural PK — no surrogate id, same precedent as
        // idempotency_records' composite natural key (CRM Revision 3).
        builder.Property(t => t.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();
        builder.HasKey(t => t.TenantId);

        builder.Property(t => t.Revision).HasDefaultValue(0L);
        builder.Property(t => t.RowVersion).IsConcurrencyToken().IsRequired();
    }
}
