using Contracts;
using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Persistence.Configurations;

public sealed class PipelineDefinitionVersionConfiguration : IEntityTypeConfiguration<PipelineDefinitionVersion>
{
    public void Configure(EntityTypeBuilder<PipelineDefinitionVersion> builder)
    {
        builder.ToTable("pipeline_definition_versions", table =>
        {
            table.HasCheckConstraint("ck_pipeline_definition_versions_status", "status IN ('Draft', 'Published', 'Superseded', 'Archived')");
            table.HasCheckConstraint("ck_pipeline_definition_versions_version_number_positive", "version_number > 0");
        });

        builder.HasKey(v => v.Id);

        builder.Property(v => v.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();
        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(16).HasDefaultValue(PipelineVersionStatus.Draft).IsRequired();

        builder.HasOne<PipelineDefinition>()
            .WithMany()
            .HasForeignKey(v => new { v.TenantId, v.PipelineDefinitionId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);

        // Stages is populated only via AddStage(), never by EF — not a queryable
        // collection. Ignore() also prevents EF from auto-discovering it as the inverse
        // of PipelineStageConfiguration's FK, which would recreate a duplicate/shadow
        // relationship (the bug this replaced).
        builder.Ignore(v => v.Stages);

        builder.HasIndex(v => new { v.TenantId, v.PipelineDefinitionId, v.VersionNumber }).IsUnique();
    }
}
