using Contracts;
using MasterData.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MasterData.Persistence.Configurations;

public sealed class PartyConfiguration : IEntityTypeConfiguration<Party>
{
    public void Configure(EntityTypeBuilder<Party> builder)
    {
        builder.ToTable("parties");

        builder.HasKey(p => p.Id);
        builder.HasAlternateKey(p => new { p.TenantId, p.Id });

        builder.Property(p => p.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(p => p.PartyType)
            .HasConversion(t => ToDb(t), t => FromDb(t))
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(p => p.Name).IsRequired();

        // Self-FK, nullable, tenant-safe composite — the merge tombstone target.
        builder.HasOne<Party>()
            .WithMany()
            .HasForeignKey(p => new { p.TenantId, p.MergedIntoPartyId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_parties_party_type", "party_type IN ('person', 'organization')"));

        builder.HasIndex(p => new { p.TenantId, p.Email });
        builder.HasIndex(p => new { p.TenantId, p.MergedIntoPartyId });
    }

    private static string ToDb(PartyType type) => type switch
    {
        PartyType.Person => "person",
        PartyType.Organization => "organization",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static PartyType FromDb(string value) => value switch
    {
        "person" => PartyType.Person,
        "organization" => PartyType.Organization,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
