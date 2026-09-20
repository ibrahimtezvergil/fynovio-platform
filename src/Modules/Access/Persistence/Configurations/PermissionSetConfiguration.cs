using Access.Domain.Authorization;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class PermissionSetConfiguration : IEntityTypeConfiguration<PermissionSet>
{
    public void Configure(EntityTypeBuilder<PermissionSet> builder)
    {
        builder.ToTable("permission_sets", AccessDbContext.AccessSchema);

        builder.HasKey(p => p.Id);

        builder.Property(p => p.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(p => p.Key).IsRequired();
        builder.Property(p => p.Name).IsRequired();
        builder.Property(p => p.Origin).HasMaxLength(20).IsRequired();
        builder.Property(p => p.OriginModuleKey).HasMaxLength(64);

        // Composite FK target for permission_set_items and role_permission_sets.
        builder.HasIndex(p => new { p.TenantId, p.Id }).IsUnique();
        builder.HasIndex(p => new { p.TenantId, p.Key }).IsUnique();

        builder.HasMany(p => p.Items)
            .WithOne()
            .HasForeignKey(i => new { i.TenantId, i.PermissionSetId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_permission_sets_origin",
            "origin IN ('tenant','system_template')"));
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_permission_sets_provenance",
            "(origin_module_key IS NULL) = (origin_version IS NULL) "
            + "AND (origin_module_key IS NULL OR origin = 'system_template') "
            + "AND (origin_version IS NULL OR origin_version >= 1)"));
    }
}
