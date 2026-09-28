using Contracts;
using CRM.Domain;
using Xunit;

namespace CRM.Tests.Domain;

public sealed class OpportunityStageHistoryEntryTests
{
    [Fact]
    public void Open_creates_an_entry_with_no_exit()
    {
        var tenant = TestData.NextTenant();
        var entry = OpportunityStageHistoryEntry.Open(tenant, opportunityId: 1, pipelineDefinitionVersionId: 2, pipelineStageId: 7);

        Assert.Equal(7, entry.PipelineStageId);
        Assert.Null(entry.ExitedAt);
    }

    [Fact]
    public void Close_sets_exited_at_once()
    {
        var tenant = TestData.NextTenant();
        var entry = OpportunityStageHistoryEntry.Open(tenant, opportunityId: 1, pipelineDefinitionVersionId: 2, pipelineStageId: 7);

        entry.Close();

        Assert.NotNull(entry.ExitedAt);
    }

    [Fact]
    public void Close_twice_throws()
    {
        var tenant = TestData.NextTenant();
        var entry = OpportunityStageHistoryEntry.Open(tenant, opportunityId: 1, pipelineDefinitionVersionId: 2, pipelineStageId: 7);
        entry.Close();

        Assert.Throws<InvalidOperationException>(() => entry.Close());
    }
}
