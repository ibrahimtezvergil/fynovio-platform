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
        // Computed value, 4dp — rounded to 2dp exactly once, at the `completed` transition.
        builder.Property(o => o.TotalAmount).HasColumnType("numeric(19,4)");
        builder.Property(o => o.CustomFields).HasColumnType("jsonb");

        // Explicit bigint concurrency token (schema revision 2, item 4) — RowVersionInterceptor
        // increments it pre-save; EF captures the original value for the UPDATE ... WHERE.
        builder.Property(o => o.RowVersion).IsConcurrencyToken().IsRequired();

        builder.HasOne<Party>()
            .WithMany()
            .HasForeignKey(o => new { o.TenantId, o.PartyId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasMany(o => o.Lines)
            .WithOne()
            .HasForeignKey(l => new { l.TenantId, l.OpportunityId })
            .HasPrincipalKey(o => new { o.TenantId, o.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_opportunities_status", "status IN ('waiting','offered','completed','canceled')");
            t.HasCheckConstraint("ck_opportunities_estimated_amount_non_negative", "estimated_amount >= 0");
            // waiting → offered is a DB-enforced gate (17 §2): expiry_date required once offered.
            t.HasCheckConstraint("ck_opportunities_expiry_required_once_offered", "status = 'waiting' OR expiry_date IS NOT NULL");
            t.HasCheckConstraint("ck_opportunities_sale_date_required_once_completed", "status <> 'completed' OR sale_date IS NOT NULL");
            t.HasCheckConstraint(
                "ck_opportunities_cancel_fields_required_once_canceled",
                "status <> 'canceled' OR (cancel_date IS NOT NULL AND cancel_reason IS NOT NULL)");
        });

        builder.HasIndex(o => new { o.TenantId, o.Status });
        builder.HasIndex(o => new { o.TenantId, o.PartyId });
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
        OpportunityStatus.Waiting => "waiting",
        OpportunityStatus.Offered => "offered",
        OpportunityStatus.Completed => "completed",
        OpportunityStatus.Canceled => "canceled",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private static OpportunityStatus FromDb(string value) => value switch
    {
        "waiting" => OpportunityStatus.Waiting,
        "offered" => OpportunityStatus.Offered,
        "completed" => OpportunityStatus.Completed,
        "canceled" => OpportunityStatus.Canceled,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
