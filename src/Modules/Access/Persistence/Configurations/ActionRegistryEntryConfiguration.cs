using Access.Domain.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class ActionRegistryEntryConfiguration : IEntityTypeConfiguration<ActionRegistryEntry>
{
    public void Configure(EntityTypeBuilder<ActionRegistryEntry> builder)
    {
        builder.ToTable("actions", AccessDbContext.AccessSchema);

        // Natural key — no surrogate id (gap-closure §2).
        builder.HasKey(a => a.ActionKey);
        builder.Property(a => a.ActionKey).HasMaxLength(200);

        builder.Property(a => a.OwnerModule).IsRequired();
        builder.Property(a => a.ResourceType).IsRequired();
        builder.Property(a => a.IsDeprecated).HasDefaultValue(false);

        // Not tenant-scoped — platform-owned catalog, same treatment as the old
        // `permissions` table (no tenant_id, no RLS).
    }
}
