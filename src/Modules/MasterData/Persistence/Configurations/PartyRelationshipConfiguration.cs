using Contracts;
using MasterData.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MasterData.Persistence.Configurations;

public sealed class PartyRelationshipConfiguration : IEntityTypeConfiguration<PartyRelationship>
{
    public void Configure(EntityTypeBuilder<PartyRelationship> builder)
    {
        builder.ToTable("party_relationships");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(r => r.RelationshipType)
            .HasConversion(t => ToDbType(t), t => FromDbType(t))
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasConversion(s => ToDbStatus(s), s => FromDbStatus(s))
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(r => r.Metadata).HasColumnType("jsonb");

        builder.HasOne<Party>()
            .WithMany()
            .HasForeignKey(r => new { r.TenantId, r.FromPartyId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Party>()
            .WithMany()
            .HasForeignKey(r => new { r.TenantId, r.ToPartyId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_party_relationships_type", "relationship_type IN ('works_for','branch_of')");
            t.HasCheckConstraint("ck_party_relationships_status", "status IN ('active','ended')");
            // Single-row invariant, same discipline as ck_opportunities_*_required_once_*.
            t.HasCheckConstraint("ck_party_relationships_ended_at_required_once_ended", "status <> 'ended' OR ended_at IS NOT NULL");
        });

        builder.HasIndex(r => new { r.TenantId, r.FromPartyId });
        builder.HasIndex(r => new { r.TenantId, r.ToPartyId });
    }

    private static string ToDbType(PartyRelationshipType type) => type switch
    {
        PartyRelationshipType.WorksFor => "works_for",
        PartyRelationshipType.BranchOf => "branch_of",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static PartyRelationshipType FromDbType(string value) => value switch
    {
        "works_for" => PartyRelationshipType.WorksFor,
        "branch_of" => PartyRelationshipType.BranchOf,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string ToDbStatus(PartyRelationshipStatus status) => status switch
    {
        PartyRelationshipStatus.Active => "active",
        PartyRelationshipStatus.Ended => "ended",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private static PartyRelationshipStatus FromDbStatus(string value) => value switch
    {
        "active" => PartyRelationshipStatus.Active,
        "ended" => PartyRelationshipStatus.Ended,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
