using Contracts;
using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Persistence.Configurations;

public sealed class OpportunityLineConfiguration : IEntityTypeConfiguration<OpportunityLine>
{
    public void Configure(EntityTypeBuilder<OpportunityLine> builder)
    {
        builder.ToTable("opportunity_lines");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        // EntityRef-shaped, no FK — Master Data doesn't own real product rows yet (doc 07 §3).
        builder.Property(l => l.ProductRefBoundedContext).IsRequired();
        builder.Property(l => l.ProductRefEntityType).IsRequired();

        builder.Property(l => l.UnitPrice).HasColumnType("numeric(19,2)");
        builder.Property(l => l.LineTotal).HasColumnType("numeric(19,4)");

        builder.Property(l => l.IsOptional).HasDefaultValue(false);
        builder.Property(l => l.IsCanceled).HasDefaultValue(false);
        builder.Property(l => l.SortOrder).HasDefaultValue(0);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_opportunity_lines_quantity_positive", "quantity > 0");
            t.HasCheckConstraint("ck_opportunity_lines_unit_price_non_negative", "unit_price >= 0");
        });

        builder.HasIndex(l => new { l.TenantId, l.OpportunityId });
    }
}
