using Access.Domain.Authentication;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class InvitationDeliveryConfiguration : IEntityTypeConfiguration<InvitationDelivery>
{
    public void Configure(EntityTypeBuilder<InvitationDelivery> builder)
    {
        builder.ToTable("invitation_deliveries", AccessDbContext.AccessSchema);
        builder.ToTable(table => table.HasCheckConstraint("ck_invitation_deliveries_state",
            "attempts >= 0 AND ((delivered_at IS NULL AND length(protected_token) > 0) OR "
            + "(delivered_at IS NOT NULL AND protected_token = ''))"));
        builder.HasKey(delivery => new { delivery.TenantId, delivery.InvitationId });
        builder.Property(delivery => delivery.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value)).IsRequired();
        builder.Property(delivery => delivery.ProtectedToken).IsRequired();
        builder.Property(delivery => delivery.NextAttemptAt).IsRequired();
        builder.HasIndex(delivery => new { delivery.TenantId, delivery.NextAttemptAt })
            .HasFilter("delivered_at IS NULL");
    }
}
