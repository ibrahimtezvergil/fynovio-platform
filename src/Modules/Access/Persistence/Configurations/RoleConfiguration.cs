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

        // NOT NULL now — the tenant_id=null "system role" model is retired
        // (round 4 Decision A, gap-closure §6).
        builder.Property(r => r.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(r => r.Key).IsRequired();
        builder.Property(r => r.Name).IsRequired();
        builder.Property(r => r.Origin).HasMaxLength(20).IsRequired();

        builder.HasIndex(r => new { r.TenantId, r.Id }).IsUnique();
        builder.HasIndex(r => new { r.TenantId, r.Key }).IsUnique();
        builder.HasIndex(r => new { r.TenantId, r.Name }).IsUnique();

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_roles_origin",
            "origin IN ('tenant','system_template')"));
    }
}
