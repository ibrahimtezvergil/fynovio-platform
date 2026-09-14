using Access.Domain.Identity;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.ToTable("tenant_memberships", AccessDbContext.IdentitySchema);

        builder.HasKey(m => m.Id);

        // No FK to a `tenants` table — none exists yet (Tenant Lifecycle module is still an
        // empty scaffold), same convention CRM's opportunities.tenant_id already uses.
        builder.Property(m => m.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(m => m.Status)
            .HasConversion(s => ToDb(s), s => FromDb(s))
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(m => m.RowVersion).IsConcurrencyToken().IsRequired();

        builder.HasIndex(m => new { m.TenantId, m.AccountId }).IsUnique();

        // Same schema (identity -> identity) — a real FK is safe here.
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(m => m.AccountId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_tenant_memberships_status",
            "status IN ('invited','active','disabled')"));
    }

    private static string ToDb(MembershipStatus status) => status switch
    {
        MembershipStatus.Invited => "invited",
        MembershipStatus.Active => "active",
        MembershipStatus.Disabled => "disabled",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private static MembershipStatus FromDb(string value) => value switch
    {
        "invited" => MembershipStatus.Invited,
        "active" => MembershipStatus.Active,
        "disabled" => MembershipStatus.Disabled,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
