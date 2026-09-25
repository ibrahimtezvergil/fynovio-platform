using Contracts;
using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Persistence.Configurations;

public sealed class CrmSettingsConfiguration : IEntityTypeConfiguration<CrmSettings>
{
    public void Configure(EntityTypeBuilder<CrmSettings> builder)
    {
        builder.ToTable("crm_settings");
        builder.HasKey(x => x.TenantId);
        builder.Property(x => x.TenantId).HasConversion(x => x.Value, x => new TenantId(x)).ValueGeneratedNever();
        builder.Property(x => x.OpportunityCreationMode).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.OpportunityCreationStepsJson).HasColumnName("opportunity_creation_steps").HasColumnType("jsonb")
            .HasDefaultValueSql("'[\"Customer\",\"Needs\",\"Products\"]'::jsonb").IsRequired();
        builder.Property(x => x.DefaultAssignmentMode).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.AssignmentPolicy).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.DefaultPrincipalIssuer).HasMaxLength(200);
        builder.Property(x => x.DefaultPrincipalSubject).HasMaxLength(200);
        builder.Property(x => x.RowVersion).IsConcurrencyToken().IsRequired();
        builder.HasOne<PipelineDefinition>().WithMany().HasForeignKey(x => new { x.TenantId, x.DefaultPipelineDefinitionId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OpportunityType>().WithMany().HasForeignKey(x => new { x.TenantId, x.DefaultOpportunityTypeId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_crm_settings_creation_mode", "opportunity_creation_mode IN ('Form', 'Wizard')");
            t.HasCheckConstraint("ck_crm_settings_creation_steps", "jsonb_typeof(opportunity_creation_steps) = 'array' AND jsonb_array_length(opportunity_creation_steps) BETWEEN 1 AND 3 AND opportunity_creation_steps @> '[\"Customer\"]'::jsonb AND opportunity_creation_steps <@ '[\"Customer\",\"Needs\",\"Products\"]'::jsonb AND (opportunity_creation_steps ->> 0) IS DISTINCT FROM (opportunity_creation_steps ->> 1) AND (opportunity_creation_steps ->> 0) IS DISTINCT FROM (opportunity_creation_steps ->> 2) AND (opportunity_creation_steps ->> 1) IS DISTINCT FROM (opportunity_creation_steps ->> 2)");
            t.HasCheckConstraint("ck_crm_settings_assignment_mode", "default_assignment_mode IN ('Manual', 'DefaultPrincipal', 'Team', 'Territory')");
            t.HasCheckConstraint("ck_crm_settings_assignment_policy", "assignment_policy IN ('AnyAssignablePrincipal', 'ManagerOnly', 'ManualOnly')");
            t.HasCheckConstraint("ck_crm_settings_default_principal_pair", "(default_principal_issuer IS NULL AND default_principal_subject IS NULL) OR (default_principal_issuer IS NOT NULL AND default_principal_subject IS NOT NULL)");
            t.HasCheckConstraint("ck_crm_settings_assignment_reference_required", "(default_assignment_mode <> 'DefaultPrincipal' OR default_principal_issuer IS NOT NULL) AND (default_assignment_mode <> 'Team' OR default_team_id IS NOT NULL) AND (default_assignment_mode <> 'Territory' OR default_territory_id IS NOT NULL)");
            t.HasCheckConstraint("ck_crm_settings_manual_assignment_policy", "assignment_policy <> 'ManualOnly' OR default_assignment_mode = 'Manual'");
        });
    }
}
