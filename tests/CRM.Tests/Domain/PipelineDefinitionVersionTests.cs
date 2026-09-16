using CRM.Domain;
using Xunit;

namespace CRM.Tests.Domain;

public sealed class PipelineDefinitionVersionTests
{
    [Fact]
    public void AddVersion_rejects_a_duplicate_version_number()
    {
        var tenant = TestData.NextTenant();
        var definition = PipelineDefinition.Create(tenant, "Sales Pipeline");
        definition.AddVersion(versionNumber: 1);

        Assert.Throws<InvalidOperationException>(() =>
            definition.AddVersion(versionNumber: 1));
    }

    [Fact]
    public void AddStage_rejects_a_duplicate_sort_order_within_one_version()
    {
        var tenant = TestData.NextTenant();
        var definition = PipelineDefinition.Create(tenant, "Sales Pipeline");
        var version = definition.AddVersion(versionNumber: 1);
        version.AddStage("Lead", sortOrder: 1);

        Assert.Throws<InvalidOperationException>(() =>
            version.AddStage("Contacted", sortOrder: 1));
    }

    [Fact]
    public void Two_versions_can_reuse_the_same_stage_names()
    {
        var tenant = TestData.NextTenant();
        var definition = PipelineDefinition.Create(tenant, "Sales Pipeline");

        var v1 = definition.AddVersion(versionNumber: 1);
        v1.AddStage("Lead", sortOrder: 1);

        var v2 = definition.AddVersion(versionNumber: 2);
        v2.AddStage("Lead", sortOrder: 1);

        Assert.Equal(2, definition.Versions.Count);
        Assert.Single(v1.Stages);
        Assert.Single(v2.Stages);
    }

    [Fact]
    public void Two_versions_can_reuse_the_same_sort_orders()
    {
        var tenant = TestData.NextTenant();
        var definition = PipelineDefinition.Create(tenant, "Sales Pipeline");

        var v1 = definition.AddVersion(versionNumber: 1);
        v1.AddStage("Lead", sortOrder: 1);
        v1.AddStage("Qualified", sortOrder: 2);

        var v2 = definition.AddVersion(versionNumber: 2);
        v2.AddStage("Prospect", sortOrder: 1);
        v2.AddStage("Customer", sortOrder: 2);

        Assert.Equal(2, v1.Stages.Count);
        Assert.Equal(2, v2.Stages.Count);
    }
}
