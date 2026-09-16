using Contracts;
using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Persistence.Configurations;

public sealed class OpportunityConfiguration : IEntityTypeConfiguration<Opportunity>
{
    public void Configure(EntityTypeBuilder<Opportunity> builder)
    {
        builder.ToTable("opportunities");

        builder.HasKey(o => o.Id);
        builder.HasAlternateKey(o => new { o.TenantId, o.Id });

        builder.Property(o => o.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(o => o.Status)
            .HasConversion(s => ToDb(s), s => FromDb(s))
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(o => o.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(o => o.EstimatedAmount).HasColumnType("numeric(19,2)");
        // Computed value, 4dp — rounded to 2dp exactly once, at the `won` transition.
        builder.Property(o => o.TotalAmount).HasColumnType("numeric(19,4)");
        builder.Property(o => o.CustomFields).HasColumnType("jsonb");

        // Explicit bigint concurrency token (schema revision 2, item 4) — Opportunity increments
        // it inside domain methods; EF captures the original value for the UPDATE ... WHERE.
        builder.Property(o => o.RowVersion).IsConcurrencyToken().IsRequired();

        // PartyRef: tenant-safety via CHECK, no cross-schema FK to MasterData.Party
        builder.Property(o => o.PartyRefPartyId).IsRequired();
        builder.Ignore(o => o.PartyRef);

        // Optional pipeline version and stage references — no enforcement yet (Phase 2's ChangePipelineStage does).
        builder.HasOne<PipelineDefinitionVersion>()
            .WithMany()
            .HasForeignKey(o => new { o.TenantId, o.PipelineDefinitionVersionId })
            .HasPrincipalKey(v => new { v.TenantId, v.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne<PipelineStage>()
            .WithMany()
            .HasForeignKey(o => new { o.TenantId, o.PipelineStageId })
            .HasPrincipalKey(s => new { s.TenantId, s.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasMany(o => o.Lines)
            .WithOne()
            .HasForeignKey(l => new { l.TenantId, l.OpportunityId })
            .HasPrincipalKey(o => new { o.TenantId, o.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_opportunities_status", "status IN ('draft','open','won','lost')");
            t.HasCheckConstraint("ck_opportunities_estimated_amount_non_negative", "estimated_amount >= 0");
            // draft → open is a DB-enforced gate (17 §2): expiry_date required once open.
            // expiry_date yalnızca open/won durumlarında zorunlu. Eski ifade
            // (status = 'draft' OR ...) teklif verilmemiş bir opportunity'nin kaybedilmesini imkansız kılıyordu.
            t.HasCheckConstraint(
                "ck_opportunities_expiry_required_once_open",
                "status NOT IN ('open','won') OR expiry_date IS NOT NULL");
            t.HasCheckConstraint("ck_opportunities_won_date_required_once_won", "status <> 'won' OR won_date IS NOT NULL");
            t.HasCheckConstraint(
                "ck_opportunities_lost_fields_required_once_lost",
                "status <> 'lost' OR (lost_date IS NOT NULL AND lost_reason IS NOT NULL)");
            t.HasCheckConstraint("ck_opportunities_party_ref_party_id_positive", "party_ref_party_id > 0");
        });

        builder.HasIndex(o => new { o.TenantId, o.Status });
        builder.HasIndex(o => new { o.TenantId, o.PartyRefPartyId });
        // Issuer+subject together, matching PrincipalRef's own identity pair (doc 19 §9
        // amendment) — an index on subject alone contradicted the primitive it indexes.
        // Explicit short name: EF Core's auto-generated name exceeds Postgres's 63-byte
        // identifier limit and gets silently truncated mid-word otherwise.
        builder.HasIndex(o => new { o.TenantId, o.AssignedPrincipalIssuer, o.AssignedPrincipalSubject })
            .HasDatabaseName("ix_opportunities_tenant_assigned_principal");
        builder.HasIndex(o => new { o.TenantId, o.CreatedAt });
    }

    private static string ToDb(OpportunityStatus status) => status switch
    {
        OpportunityStatus.Draft => "draft",
        OpportunityStatus.Open => "open",
        OpportunityStatus.Won => "won",
        OpportunityStatus.Lost => "lost",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private static OpportunityStatus FromDb(string value) => value switch
    {
        "draft" => OpportunityStatus.Draft,
        "open" => OpportunityStatus.Open,
        "won" => OpportunityStatus.Won,
        "lost" => OpportunityStatus.Lost,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
