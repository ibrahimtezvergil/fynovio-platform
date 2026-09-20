using Access.Domain.Authorization;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment>
{
    public void Configure(EntityTypeBuilder<RoleAssignment> builder)
    {
        builder.ToTable("role_assignments", AccessDbContext.AccessSchema);

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(r => r.PrincipalType)
            .HasConversion(p => p.ToString().ToLowerInvariant(), v => Enum.Parse<PrincipalType>(v, ignoreCase: true))
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(r => r.Source).HasMaxLength(24).IsRequired();
        builder.Property(r => r.Reason).HasMaxLength(500);
        builder.Property(r => r.RowVersion).IsConcurrencyToken().IsRequired();

        // account_id/granted_by_account_id: no FK across the access/identity schema
        // boundary — same "reference by value" convention as before (identity-access-schema.md).
        builder.HasIndex(r => new { r.TenantId, r.AccountId });

        // Composite tenant-safe FK now (AGENTS.md binding-core #1) — was a bare FK before.
        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(r => new { r.TenantId, r.RoleId })
            .HasPrincipalKey(role => new { role.TenantId, role.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_role_assignments_principal_type",
            "principal_type = 'user'"));
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_role_assignments_source",
            "source IN ('manual','bootstrap','module_enablement')"));
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_role_assignments_valid_range",
            "valid_to IS NULL OR valid_to > valid_from"));
    }
}
