using Contracts;
using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Persistence.Configurations;

public sealed class PartyConfiguration : IEntityTypeConfiguration<Party>
{
    public void Configure(EntityTypeBuilder<Party> builder)
    {
        builder.ToTable("parties");

        builder.HasKey(p => p.Id);
        // UNIQUE(tenant_id, id) — target of every tenant-safe composite FK to this table.
        builder.HasAlternateKey(p => new { p.TenantId, p.Id });

        builder.Property(p => p.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(p => p.Name).IsRequired();

        builder.Property(p => p.CreationSource)
            .HasConversion(s => ToDb(s), s => FromDb(s))
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(p => p.CustomFields).HasColumnType("jsonb");

        // self-FK, nullable, tenant-safe composite (AI-record merge target).
        builder.HasOne<Party>()
            .WithMany()
            .HasForeignKey(p => new { p.TenantId, p.MergedIntoPartyId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_parties_creation_source",
            "creation_source IN ('manual', 'ai_voice_capture')"));

        builder.HasIndex(p => new { p.TenantId, p.Email });
    }

    private static string ToDb(PartyCreationSource source) => source switch
    {
        PartyCreationSource.Manual => "manual",
        PartyCreationSource.AiVoiceCapture => "ai_voice_capture",
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };

    private static PartyCreationSource FromDb(string value) => value switch
    {
        "manual" => PartyCreationSource.Manual,
        "ai_voice_capture" => PartyCreationSource.AiVoiceCapture,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
