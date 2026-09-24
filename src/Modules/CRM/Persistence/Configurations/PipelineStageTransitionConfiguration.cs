using Contracts;
using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Persistence.Configurations;

public sealed class PipelineStageTransitionConfiguration : IEntityTypeConfiguration<PipelineStageTransition>
{
    public void Configure(EntityTypeBuilder<PipelineStageTransition> builder)
    {
        builder.ToTable("pipeline_stage_transitions", table => table.HasCheckConstraint("ck_pipeline_stage_transitions_distinct_stages", "from_stage_id <> to_stage_id"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).HasConversion(x => x.Value, x => new TenantId(x)).IsRequired();
        builder.HasOne<PipelineDefinitionVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.PipelineDefinitionVersionId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PipelineStage>().WithMany().HasForeignKey(x => new { x.TenantId, x.PipelineDefinitionVersionId, x.FromStageId }).HasPrincipalKey(x => new { x.TenantId, x.PipelineDefinitionVersionId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PipelineStage>().WithMany().HasForeignKey(x => new { x.TenantId, x.PipelineDefinitionVersionId, x.ToStageId }).HasPrincipalKey(x => new { x.TenantId, x.PipelineDefinitionVersionId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.PipelineDefinitionVersionId, x.FromStageId, x.ToStageId }).IsUnique();
    }
}
