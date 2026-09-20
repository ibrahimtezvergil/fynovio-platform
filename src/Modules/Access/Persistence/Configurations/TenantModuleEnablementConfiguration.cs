using Access.Domain.Authorization;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class TenantModuleEnablementConfiguration : IEntityTypeConfiguration<TenantModuleEnablement>
{
    public void Configure(EntityTypeBuilder<TenantModuleEnablement> builder)
    {
        builder.ToTable("tenant_module_enablements", AccessDbContext.AccessSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();
        builder.Property(e => e.ModuleKey).HasMaxLength(64).IsRequired();
        builder.Property(e => e.TemplateVersion).IsRequired();
        builder.Property(e => e.EnabledAt).IsRequired();

        // One enablement per (tenant, module): the natural idempotency key of the enable command.
        builder.HasIndex(e => new { e.TenantId, e.ModuleKey }).IsUnique();

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_tenant_module_enablements_version",
            "template_version >= 1"));
    }
}
