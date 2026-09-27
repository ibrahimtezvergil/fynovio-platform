using Contracts;
using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Persistence.Configurations;

public sealed class PipelineStageConfiguration : IEntityTypeConfiguration<PipelineStage>
{
    public void Configure(EntityTypeBuilder<PipelineStage> builder)
    {
        builder.ToTable("pipeline_stages", table =>
        {
            table.HasCheckConstraint("ck_pipeline_stages_sort_order_non_negative", "sort_order >= 0");
            table.HasCheckConstraint("ck_pipeline_stages_name_non_empty", "length(btrim(name)) > 0");
            table.HasCheckConstraint("ck_pipeline_stages_archived_inactive", "NOT is_archived OR NOT is_active");
            table.HasCheckConstraint("ck_pipeline_stages_kind", "kind IN ('open','won','lost')");
        });

        builder.HasKey(s => s.Id);
        builder.HasAlternateKey(s => new { s.TenantId, s.PipelineDefinitionVersionId, s.Id });

        builder.Property(s => s.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(s => s.Name).IsRequired();
        builder.Property(s => s.IsActive).HasDefaultValue(true).IsRequired();
        builder.Property(s => s.IsArchived).HasDefaultValue(false).IsRequired();
        builder.Property(s => s.IsEntry).HasDefaultValue(false).IsRequired();

        builder.Property(s => s.Kind)
            .HasConversion(k => ToDb(k), v => FromDb(v))
            .HasMaxLength(8)
            .HasDefaultValue(PipelineStageKind.Open)
            .IsRequired();

        builder.HasOne<PipelineDefinitionVersion>()
            .WithMany()
            .HasForeignKey(s => new { s.TenantId, s.PipelineDefinitionVersionId })
            .HasPrincipalKey(v => new { v.TenantId, v.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.TenantId, s.PipelineDefinitionVersionId, s.SortOrder }).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.PipelineDefinitionVersionId, s.Name }).IsUnique();

        // Partial unique index: at most one entry stage per version. A tenant with zero
        // entry stages configured is allowed (OpenOpportunity then assigns no stage) —
        // only "more than one" is forbidden.
        builder.HasIndex(s => new { s.TenantId, s.PipelineDefinitionVersionId })
            .HasDatabaseName("ux_pipeline_stages_one_entry_per_version")
            .IsUnique()
            .HasFilter("is_entry = true");
    }

    private static string ToDb(PipelineStageKind kind) => kind switch
    {
        PipelineStageKind.Open => "open",
        PipelineStageKind.Won => "won",
        PipelineStageKind.Lost => "lost",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static PipelineStageKind FromDb(string value) => value switch
    {
        "open" => PipelineStageKind.Open,
        "won" => PipelineStageKind.Won,
        "lost" => PipelineStageKind.Lost,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
