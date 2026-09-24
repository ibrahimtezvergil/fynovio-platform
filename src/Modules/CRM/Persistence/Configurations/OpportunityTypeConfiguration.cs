using Contracts;
using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Persistence.Configurations;

public sealed class OpportunityTypeConfiguration : IEntityTypeConfiguration<OpportunityType>
{
    public void Configure(EntityTypeBuilder<OpportunityType> builder)
    {
        builder.ToTable("opportunity_types", table =>
        {
            table.HasCheckConstraint("ck_opportunity_types_status", "status IN ('Active', 'Inactive', 'Archived')");
            table.HasCheckConstraint("ck_opportunity_types_key_name_non_empty", "length(btrim(key)) > 0 AND length(btrim(name)) > 0");
        });
        builder.HasKey(x => x.Id); builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.Property(x => x.TenantId).HasConversion(x => x.Value, x => new TenantId(x)).IsRequired();
        builder.Property(x => x.Key).HasMaxLength(100).IsRequired(); builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.RowVersion).IsConcurrencyToken().IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
    }
}
