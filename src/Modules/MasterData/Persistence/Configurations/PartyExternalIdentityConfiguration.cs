using Contracts;
using MasterData.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MasterData.Persistence.Configurations;

public sealed class PartyExternalIdentityConfiguration : IEntityTypeConfiguration<PartyExternalIdentity>
{
    public void Configure(EntityTypeBuilder<PartyExternalIdentity> builder)
    {
        builder.ToTable("party_external_identities");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.HasOne<Party>()
            .WithMany()
            .HasForeignKey(e => new { e.TenantId, e.PartyId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);

        // Postgres unique indexes treat NULL <> NULL, so a plain UNIQUE including the
        // nullable external_type would silently allow duplicates whenever it's NULL —
        // the common case (design review caught this; see
        // docs/plans/2026-09-16-masterdata-party-foundation.md §6). A stored computed
        // shadow column normalizes NULL to '' so the index means something, and stays
        // fully EF-generated — no hand-written migration needed for this, unlike RLS.
        builder.Property<string>("ExternalTypeKey")
            .HasComputedColumnSql("COALESCE(external_type, '')", stored: true);

        builder.HasIndex("TenantId", "SourceInstanceRef", "ExternalTypeKey", "ExternalId")
            .IsUnique()
            .HasDatabaseName("ux_party_external_identities_tenant_source_type_external_id");

        builder.HasIndex(e => new { e.TenantId, e.PartyId });
    }
}
