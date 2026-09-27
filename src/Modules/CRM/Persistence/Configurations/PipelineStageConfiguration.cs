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

        // Kind will be configured in Task 2 with proper column type and CHECK constraint
        builder.Ignore(s => s.Kind);
    }
}
