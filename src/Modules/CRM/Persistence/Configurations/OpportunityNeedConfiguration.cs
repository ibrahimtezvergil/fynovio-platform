using Contracts;
using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Persistence.Configurations;

public sealed class OpportunityNeedConfiguration : IEntityTypeConfiguration<OpportunityNeed>
{
    public void Configure(EntityTypeBuilder<OpportunityNeed> builder)
    {
        builder.ToTable("opportunity_needs");

        builder.HasKey(n => new { n.TenantId, n.OpportunityId, n.CustomerNeedId });

        builder.Property(n => n.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.HasOne<Opportunity>()
            .WithMany()
            .HasForeignKey(n => new { n.TenantId, n.OpportunityId })
            .HasPrincipalKey(o => new { o.TenantId, o.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<CustomerNeed>()
            .WithMany()
            .HasForeignKey(n => new { n.TenantId, n.CustomerNeedId })
            .HasPrincipalKey(c => new { c.TenantId, c.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
