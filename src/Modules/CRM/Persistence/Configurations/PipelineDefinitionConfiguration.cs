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
        builder.ToTable(t => t.HasCheckConstraint("ck_pipeline_definitions_name_non_empty", "length(btrim(name)) > 0"));

        builder.HasKey(p => p.Id);
        builder.HasAlternateKey(p => new { p.TenantId, p.Id });

        builder.Property(p => p.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(p => p.Name).IsRequired();
        builder.Property(p => p.RowVersion).IsConcurrencyToken().IsRequired();
        builder.Property(p => p.IsActive).HasDefaultValue(true).IsRequired();
        builder.Property(p => p.IsArchived).HasDefaultValue(false).IsRequired();

        // Versions is populated only via AddVersion(), never by EF — not a queryable
        // collection. Ignore() also prevents EF from auto-discovering it as the inverse
        // of PipelineDefinitionVersionConfiguration's FK, which would recreate a
        // duplicate/shadow relationship.
        builder.Ignore(p => p.Versions);

        builder.HasIndex(p => new { p.TenantId, p.Name }).IsUnique();
    }
}
