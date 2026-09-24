using Contracts;
using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Persistence.Configurations;

public sealed class CustomerNeedConfiguration : IEntityTypeConfiguration<CustomerNeed>
{
    public void Configure(EntityTypeBuilder<CustomerNeed> builder)
    {
        builder.ToTable("customer_needs");

        builder.HasKey(c => c.Id);
        builder.HasAlternateKey(c => new { c.TenantId, c.Id });

        builder.Property(c => c.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(c => c.Name).IsRequired();
        builder.Property(c => c.Category).HasMaxLength(100);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(16).HasDefaultValue(ConfigurationStatus.Active).IsRequired();
        builder.Property(c => c.RowVersion).IsConcurrencyToken().IsRequired();

        // Entered value, 2dp (docs/schema/crm-sales-schema.md — one money-rounding rule).
        builder.Property(c => c.AveragePrice).HasColumnType("numeric(19,2)");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_customer_needs_average_price_non_negative", "average_price >= 0");
            t.HasCheckConstraint("ck_customer_needs_status", "status IN ('Active', 'Inactive', 'Archived')");
            t.HasCheckConstraint("ck_customer_needs_name_non_empty", "length(btrim(name)) > 0");
        });
    }
}
