using Access.Domain.Authorization;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles", AccessDbContext.AccessSchema);

        builder.HasKey(r => r.Id);

        // Nullable: NULL = platform system role. No FK to a `tenants` table — none exists
        // yet, same convention as tenant_memberships.tenant_id.
        builder.Property(r => r.TenantId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (long?)null,
                value => value.HasValue ? new TenantId(value.Value) : (TenantId?)null);

        builder.Property(r => r.Name).IsRequired();
        builder.Property(r => r.IsSystem).HasDefaultValue(false);
    }
}
