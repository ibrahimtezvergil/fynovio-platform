using System.Reflection;
using CRM.Domain;
using Xunit;

namespace CRM.Tests.Domain;

/// <summary>CRM_Phase1_Test_Coverage_Verification_Report.pdf F-04: a Stage-belongs-to-Version
/// consistency check would guard against `PipelineStageId` and `PipelineDefinitionVersionId`
/// disagreeing on which pipeline they point to. There is nothing to write today — both
/// properties are `private set` and no public command assigns them (docs/schema/crm-sales-schema.md
/// item 19: "reserved for a later phase's ChangePipelineStage"), so the mismatch this would
/// guard against cannot currently be constructed. This regression-lock test fails the moment
/// that stops being true, which is the trigger for adding the real consistency check — see
/// the Phase 2 prerequisite note in docs/plans/crm-phase1/2026-09-16-crm-target-model-phase0-delta-plan.md.</summary>
public sealed class OpportunityPipelineFieldsTests
{
    [Fact]
    public void PipelineDefinitionVersionId_and_PipelineStageId_have_no_public_setter()
    {
        var versionIdProperty = typeof(Opportunity).GetProperty(nameof(Opportunity.PipelineDefinitionVersionId))!;
        var stageIdProperty = typeof(Opportunity).GetProperty(nameof(Opportunity.PipelineStageId))!;

        Assert.False(versionIdProperty.SetMethod?.IsPublic ?? false);
        Assert.False(stageIdProperty.SetMethod?.IsPublic ?? false);
    }

    [Fact]
    public void No_public_command_assigns_a_pipeline_stage_or_version()
    {
        var publicMethods = typeof(Opportunity)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName); // exclude property get_/set_ accessors

        Assert.DoesNotContain(publicMethods, m =>
            m.Name.Contains("Stage", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("Pipeline", StringComparison.OrdinalIgnoreCase));
    }
}
