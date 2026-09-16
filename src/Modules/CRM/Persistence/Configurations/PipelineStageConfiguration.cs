using Contracts;
using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Persistence.Configurations;

public sealed class PipelineStageConfiguration : IEntityTypeConfiguration<PipelineStage>
{
    public void Configure(EntityTypeBuilder<PipelineStage> builder)
    {
        builder.ToTable("pipeline_stages");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(s => s.Name).IsRequired();

        builder.HasOne<PipelineDefinitionVersion>()
            .WithMany()
            .HasForeignKey(s => new { s.TenantId, s.PipelineDefinitionVersionId })
            .HasPrincipalKey(v => new { v.TenantId, v.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.TenantId, s.PipelineDefinitionVersionId, s.SortOrder }).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.PipelineDefinitionVersionId, s.Name }).IsUnique();
    }
}
