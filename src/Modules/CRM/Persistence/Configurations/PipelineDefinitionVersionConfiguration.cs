using Contracts;
using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Persistence.Configurations;

public sealed class PipelineDefinitionVersionConfiguration : IEntityTypeConfiguration<PipelineDefinitionVersion>
{
    public void Configure(EntityTypeBuilder<PipelineDefinitionVersion> builder)
    {
        builder.ToTable("pipeline_definition_versions");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.HasOne<PipelineDefinition>()
            .WithMany()
            .HasForeignKey(v => new { v.TenantId, v.PipelineDefinitionId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(v => v.Stages)
            .WithOne()
            .HasForeignKey(s => new { s.TenantId, s.PipelineDefinitionVersionId })
            .HasPrincipalKey(v => new { v.TenantId, v.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => new { v.TenantId, v.PipelineDefinitionId, v.VersionNumber }).IsUnique();
    }
}
