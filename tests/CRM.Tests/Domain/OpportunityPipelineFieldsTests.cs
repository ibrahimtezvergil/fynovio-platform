using Contracts;
using CRM.Domain;
using Xunit;

namespace CRM.Tests.Domain;

public sealed class OpportunityPipelineFieldsTests
{
    private static Opportunity NewDraftOpportunity()
    {
        var tenant = TestData.NextTenant();
        return Opportunity.Create(tenant, new PartyRef(tenant, 1), TestData.Seller, "TRY", estimatedAmount: 1000m);
    }

    [Fact]
    public void Open_assigns_the_supplied_pipeline_version_and_stage()
    {
        var opportunity = NewDraftOpportunity();

        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: 10, pipelineStageId: 20);

        Assert.Equal(10, opportunity.PipelineDefinitionVersionId);
        Assert.Equal(20, opportunity.PipelineStageId);
    }

    [Fact]
    public void Open_with_no_pipeline_configured_leaves_both_fields_null()
    {
        var opportunity = NewDraftOpportunity();

        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: null, pipelineStageId: null);

        Assert.Null(opportunity.PipelineDefinitionVersionId);
        Assert.Null(opportunity.PipelineStageId);
    }

    [Fact]
    public void Open_rejects_a_stage_supplied_without_its_version()
    {
        var opportunity = NewDraftOpportunity();

        Assert.Throws<ArgumentException>(() =>
            opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: null, pipelineStageId: 20));
    }

    [Fact]
    public void No_command_other_than_Open_assigns_a_pipeline_stage_or_version()
    {
        // Narrowed from the Phase 1 version of this test (architecture plan §2.3): Open()
        // is now the one, explicit exception. Every other command must still never touch
        // these fields.
        var tenant = TestData.NextTenant();
        var opportunity = Opportunity.Create(tenant, new PartyRef(tenant, 1), TestData.Seller, "TRY", 1000m);
        opportunity.AddLine(TestData.ProductRef(tenant), quantity: 1, unitPrice: 10m);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: null, pipelineStageId: null);
        opportunity.Win();

        Assert.Null(opportunity.PipelineDefinitionVersionId);
        Assert.Null(opportunity.PipelineStageId);
    }
}
