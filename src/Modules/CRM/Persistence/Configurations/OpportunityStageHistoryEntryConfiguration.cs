using Contracts;
using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Persistence.Configurations;

public sealed class OpportunityStageHistoryEntryConfiguration : IEntityTypeConfiguration<OpportunityStageHistoryEntry>
{
    public void Configure(EntityTypeBuilder<OpportunityStageHistoryEntry> builder)
    {
        builder.ToTable("opportunity_stage_history");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.HasOne<Opportunity>()
            .WithMany()
            .HasForeignKey(e => new { e.TenantId, e.OpportunityId })
            .HasPrincipalKey(o => new { o.TenantId, o.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<PipelineStage>()
            .WithMany()
            .HasForeignKey(e => new { e.TenantId, e.PipelineDefinitionVersionId, e.PipelineStageId })
            .HasPrincipalKey(s => new { s.TenantId, s.PipelineDefinitionVersionId, s.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.TenantId, e.OpportunityId, e.EnteredAt });
    }
}
