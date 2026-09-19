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

    [Fact]
    public void AddStage_the_first_stage_added_becomes_entry_by_default()
    {
        var tenant = TestData.NextTenant();
        var definition = PipelineDefinition.Create(tenant, "Sales Pipeline");
        var version = definition.AddVersion(versionNumber: 1);

        var stage = version.AddStage("Bekliyor", sortOrder: 0);

        Assert.True(stage.IsEntry);
        Assert.True(stage.IsActive);
    }

    [Fact]
    public void AddStage_a_second_stage_is_not_entry_by_default()
    {
        var tenant = TestData.NextTenant();
        var definition = PipelineDefinition.Create(tenant, "Sales Pipeline");
        var version = definition.AddVersion(versionNumber: 1);
        version.AddStage("Bekliyor", sortOrder: 0);

        var second = version.AddStage("Teklif Verildi", sortOrder: 1);

        Assert.False(second.IsEntry);
    }

    /// <summary>The discriminating test the 2026-09-19 entry-stage resolution names
    /// directly: SortOrder is presentation ordering only and must never be read as an
    /// entry-stage signal. Entry is decided once, at first-add, and never revisited by
    /// a later AddStage call regardless of that stage's SortOrder being lower.</summary>
    [Fact]
    public void AddStage_a_later_stage_with_a_lower_sort_order_does_not_become_entry()
    {
        var tenant = TestData.NextTenant();
        var definition = PipelineDefinition.Create(tenant, "Sales Pipeline");
        var version = definition.AddVersion(versionNumber: 1);
        var first = version.AddStage("Bekliyor", sortOrder: 5);

        var earlier = version.AddStage("Ön Görüşme", sortOrder: 0);

        Assert.True(first.IsEntry);
        Assert.False(earlier.IsEntry);
    }

    [Fact]
    public void MarkEntry_moves_the_entry_flag_to_the_target_stage_only()
    {
        var tenant = TestData.NextTenant();
        var definition = PipelineDefinition.Create(tenant, "Sales Pipeline");
        var version = definition.AddVersion(versionNumber: 1);
        var first = version.AddStage("Bekliyor", sortOrder: 0);
        var second = version.AddStage("Teklif Verildi", sortOrder: 1);

        version.MarkEntry(second);

        Assert.False(first.IsEntry);
        Assert.True(second.IsEntry);
    }
}
