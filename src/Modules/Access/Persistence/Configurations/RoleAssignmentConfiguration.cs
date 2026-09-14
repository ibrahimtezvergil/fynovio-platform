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

        // No FK to a `tenants` table — same convention as tenant_memberships.tenant_id.
        builder.Property(r => r.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(r => r.ScopeType)
            .HasConversion(s => ToDb(s), s => FromDb(s))
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(r => r.RowVersion).IsConcurrencyToken().IsRequired();

        // account_id is deliberately NOT a real FK: this table lives in the `access` schema,
        // accounts in `identity`. Identity+Access are one assembly only as a pilot exception
        // (doc 08 §2) with "explicit internal ownership" — a cross-schema FK here would
        // survive that merge and block a future split. Same EntityRef/PrincipalRef-style
        // "reference by value, no FK across the boundary" convention CRM already uses for
        // assigned_principal_issuer/subject -> external_identities.
        builder.HasIndex(r => new { r.TenantId, r.AccountId });

        // role_id stays a real FK — both role_assignments and roles are in `access`.
        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(r => r.RoleId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_role_assignments_scope_type",
            "scope_type IN ('tenant','organization_unit','network')"));
    }

    private static string ToDb(RoleAssignmentScopeType scope) => scope switch
    {
        RoleAssignmentScopeType.Tenant => "tenant",
        RoleAssignmentScopeType.OrganizationUnit => "organization_unit",
        RoleAssignmentScopeType.Network => "network",
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };

    private static RoleAssignmentScopeType FromDb(string value) => value switch
    {
        "tenant" => RoleAssignmentScopeType.Tenant,
        "organization_unit" => RoleAssignmentScopeType.OrganizationUnit,
        "network" => RoleAssignmentScopeType.Network,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
