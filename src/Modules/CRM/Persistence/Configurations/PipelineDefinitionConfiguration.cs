using Contracts;
using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Persistence.Configurations;

public sealed class PipelineDefinitionConfiguration : IEntityTypeConfiguration<PipelineDefinition>
{
    public void Configure(EntityTypeBuilder<PipelineDefinition> builder)
    {
        builder.ToTable("pipeline_definitions");

        builder.HasKey(p => p.Id);
        builder.HasAlternateKey(p => new { p.TenantId, p.Id });

        builder.Property(p => p.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(p => p.Name).IsRequired();

        builder.Ignore(p => p.Versions);

        builder.HasIndex(p => new { p.TenantId, p.Name }).IsUnique();
    }
}
