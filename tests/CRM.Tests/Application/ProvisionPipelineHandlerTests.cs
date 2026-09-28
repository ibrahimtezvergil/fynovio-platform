using Contracts;
using CRM.Application;
using CRM.Domain;
using CRM.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Application;

[Collection(nameof(PostgresCollection))]
public sealed class ProvisionPipelineHandlerTests
{
    private readonly PostgresFixture _fixture;

    public ProvisionPipelineHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<ProvisionPipelineResult> ProvisionAsync(TenantId tenant, string name, IReadOnlyList<string> stages, IReadOnlyList<string>? retired = null)
    {
        await using var context = _fixture.CreateAdminContext();
        return await new ProvisionPipelineHandler(context).HandleAsync(new ProvisionPipelineCommand(tenant, name, stages, retired));
    }

    [Fact]
    public async Task Provisions_version_one_with_the_operators_stages_in_order_and_the_first_as_the_only_entry()
    {
        var tenant = TestData.NextTenant();

        var result = await ProvisionAsync(tenant, "  Sales pipeline ", [" Qualification", "Proposal ", "Negotiation"]);

        Assert.Equal(ProvisionPipelineStatus.Provisioned, result.Status);
        await using var read = _fixture.CreateAdminContext();
        var definition = await read.PipelineDefinitions.AsNoTracking().SingleAsync(p => p.TenantId == tenant);
        Assert.Equal("Sales pipeline", definition.Name);
        var version = await read.PipelineDefinitionVersions.AsNoTracking().SingleAsync(v => v.TenantId == tenant);
        Assert.Equal(1, version.VersionNumber);
        Assert.Equal(version.Id, result.PipelineDefinitionVersionId);
        var stages = await read.PipelineStages.AsNoTracking().Where(s => s.TenantId == tenant).OrderBy(s => s.SortOrder).ToListAsync();
        Assert.Equal(["Qualification", "Proposal", "Negotiation", "Won", "Lost"], stages.Select(s => s.Name));
        Assert.Equal([true, false, false, false, false], stages.Select(s => s.IsEntry));
        Assert.Equal([true, true, true, true, true], stages.Select(s => s.IsActive));
    }

    [Fact]
    public async Task A_second_run_changes_nothing_even_when_asked_for_different_stages()
    {
        var tenant = TestData.NextTenant();
        await ProvisionAsync(tenant, "Sales", ["A", "B"]);

        var again = await ProvisionAsync(tenant, "Other name", ["X", "Y", "Z"]);

        Assert.Equal(ProvisionPipelineStatus.AlreadyProvisioned, again.Status);
        await using var read = _fixture.CreateAdminContext();
        Assert.Equal("Sales", (await read.PipelineDefinitions.AsNoTracking().SingleAsync(p => p.TenantId == tenant)).Name);
        Assert.Equal(["A", "B", "Won", "Lost"], (await read.PipelineStages.AsNoTracking().Where(s => s.TenantId == tenant).OrderBy(s => s.SortOrder).ToListAsync()).Select(s => s.Name));
    }

    [Fact]
    public async Task Retired_stages_are_created_inactive_after_the_active_ones_and_never_as_the_entry()
    {
        var tenant = TestData.NextTenant();

        await ProvisionAsync(tenant, "Sales", ["A", "B"], retired: ["Legacy"]);

        await using var read = _fixture.CreateAdminContext();
        var stages = await read.PipelineStages.AsNoTracking().Where(s => s.TenantId == tenant).OrderBy(s => s.SortOrder).ToListAsync();
        Assert.Equal(["A", "B", "Legacy", "Won", "Lost"], stages.Select(s => s.Name));
        Assert.Equal([true, true, false, true, true], stages.Select(s => s.IsActive));
        Assert.Equal([true, false, false, false, false], stages.Select(s => s.IsEntry));
    }

    [Theory]
    [InlineData("", new[] { "A" })]
    [InlineData("  ", new[] { "A" })]
    [InlineData("Sales", new string[0])]
    [InlineData("Sales", new[] { "A", " " })]
    [InlineData("Sales", new[] { "A", "a" })]
    public async Task Invalid_input_is_refused_and_writes_nothing(string name, string[] stages)
    {
        var tenant = TestData.NextTenant();

        await Assert.ThrowsAsync<ArgumentException>(() => ProvisionAsync(tenant, name, stages));

        await using var read = _fixture.CreateAdminContext();
        Assert.Empty(await read.PipelineDefinitions.AsNoTracking().Where(p => p.TenantId == tenant).ToListAsync());
    }

    [Fact]
    public async Task A_retired_stage_cannot_repeat_an_active_one()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => ProvisionAsync(TestData.NextTenant(), "Sales", ["A"], retired: ["a"]));
    }

    [Fact]
    public async Task One_tenants_pipeline_does_not_block_another_and_the_runtime_role_sees_only_its_own()
    {
        var first = TestData.NextTenant();
        var second = TestData.NextTenant();
        await ProvisionAsync(first, "First", ["A"]);

        // Provisioned under the RLS runtime role: the tenant GUC must be set by the handler itself.
        await using var runtime = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        var result = await new ProvisionPipelineHandler(runtime).HandleAsync(new ProvisionPipelineCommand(second, "Second", ["B", "C"]));

        Assert.Equal(ProvisionPipelineStatus.Provisioned, result.Status);
        await using var read = _fixture.CreateAdminContext();
        Assert.Equal(["First"], (await read.PipelineDefinitions.AsNoTracking().Where(p => p.TenantId == first).ToListAsync()).Select(p => p.Name));
        Assert.Equal(["Second"], (await read.PipelineDefinitions.AsNoTracking().Where(p => p.TenantId == second).ToListAsync()).Select(p => p.Name));
    }

    [Fact]
    public async Task HandleAsync_gives_the_new_pipeline_a_won_and_a_lost_stage()
    {
        var tenant = TestData.NextTenant();
        var handler = new ProvisionPipelineHandler(_fixture.CreateAdminContext());

        var result = await handler.HandleAsync(new ProvisionPipelineCommand(tenant, "Sales", ["Open"], RetiredStageNames: []));

        Assert.Equal(ProvisionPipelineStatus.Provisioned, result.Status);
        await using var read = _fixture.CreateAdminContext();
        var stages = await read.PipelineStages.Where(s => s.TenantId == tenant && s.PipelineDefinitionVersionId == result.PipelineDefinitionVersionId).ToListAsync();
        Assert.Contains(stages, s => s.Kind == PipelineStageKind.Won);
        Assert.Contains(stages, s => s.Kind == PipelineStageKind.Lost);
    }
}
