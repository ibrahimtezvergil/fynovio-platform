using Access.Domain.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions", AccessDbContext.AccessSchema);

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Key).IsRequired();

        builder.HasIndex(p => p.Key).IsUnique();
    }
}
