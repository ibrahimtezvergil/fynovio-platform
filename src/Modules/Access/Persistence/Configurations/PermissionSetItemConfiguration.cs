using Access.Domain.Authorization;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class PermissionSetItemConfiguration : IEntityTypeConfiguration<PermissionSetItem>
{
    public void Configure(EntityTypeBuilder<PermissionSetItem> builder)
    {
        builder.ToTable("permission_set_items", AccessDbContext.AccessSchema);

        builder.HasKey(i => i.Id);

        builder.Property(i => i.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(i => i.ActionKey).IsRequired();
        builder.Property(i => i.Relation).HasMaxLength(20);

        builder.HasOne<ActionRegistryEntry>()
            .WithMany()
            .HasForeignKey(i => i.ActionKey)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.TenantId, i.ActionKey });

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_permission_set_items_relation",
            "relation IS NULL OR relation = 'owner'"));
    }
}
