using Access.Domain.Authorization;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class RolePermissionSetConfiguration : IEntityTypeConfiguration<RolePermissionSet>
{
    public void Configure(EntityTypeBuilder<RolePermissionSet> builder)
    {
        builder.ToTable("role_permission_sets", AccessDbContext.AccessSchema);

        builder.Property(rp => rp.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.HasKey(rp => new { rp.RoleId, rp.PermissionSetId });

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(rp => new { rp.TenantId, rp.RoleId })
            .HasPrincipalKey(r => new { r.TenantId, r.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<PermissionSet>()
            .WithMany()
            .HasForeignKey(rp => new { rp.TenantId, rp.PermissionSetId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
