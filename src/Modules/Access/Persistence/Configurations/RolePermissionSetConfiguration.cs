using Access.Domain.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class RolePermissionSetConfiguration : IEntityTypeConfiguration<RolePermissionSet>
{
    public void Configure(EntityTypeBuilder<RolePermissionSet> builder)
    {
        builder.ToTable("role_permission_sets", AccessDbContext.AccessSchema);

        builder.HasKey(rp => new { rp.RoleId, rp.PermissionSetId });

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<PermissionSet>()
            .WithMany()
            .HasForeignKey(rp => rp.PermissionSetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
