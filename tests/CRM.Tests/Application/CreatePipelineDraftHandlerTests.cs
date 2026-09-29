using Contracts;
using CRM.Application;
using CRM.Domain;
using CRM.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Application;

[Collection(nameof(PostgresCollection))]
public sealed class CreatePipelineDraftHandlerTests
{
    private readonly PostgresFixture _fixture;

    public CreatePipelineDraftHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    private static readonly PrincipalRef Administrator = new("https://identity.test", "admin");

    [Fact]
    public async Task Publishing_a_new_draft_always_includes_won_and_lost_stages()
    {
        var tenantId = TestData.NextTenant();
        var correlationId = Guid.NewGuid();
        CreatePipelineDraftResult draft;
        await using (var context = _fixture.CreateAdminContext())
        {
            draft = await new CreatePipelineDraftHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(
                Draft(tenantId, correlationId, null, 0, 0, "draft-with-stages",
                    [new("Entry", 1, true, true)]));
        }

        var versionId = draft.VersionId;
        await using var read = _fixture.CreateAdminContext();
        var stages = await read.PipelineStages.Where(s => s.TenantId == tenantId && s.PipelineDefinitionVersionId == versionId).ToListAsync();
        Assert.Single(stages, s => s.Kind == PipelineStageKind.Won);
        Assert.Single(stages, s => s.Kind == PipelineStageKind.Lost);
    }

    [Fact]
    public async Task Validate_rejects_an_operator_supplied_stage_named_won_or_lost()
    {
        var tenantId = TestData.NextTenant();
        await using var context = _fixture.CreateAdminContext();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new CreatePipelineDraftHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(
                Draft(tenantId, Guid.NewGuid(), null, 0, 0, "draft-won-stage",
                    [new("Won", 1, true, true)])));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new CreatePipelineDraftHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(
                Draft(tenantId, Guid.NewGuid(), null, 0, 0, "draft-lost-stage",
                    [new("Lost", 1, true, true)])));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new CreatePipelineDraftHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(
                Draft(tenantId, Guid.NewGuid(), null, 0, 0, "draft-won-case-insensitive",
                    [new("won", 1, true, true)])));
    }

    [Fact]
    public async Task The_tenants_labels_for_the_won_and_lost_system_stages_are_kept_and_they_still_sort_last()
    {
        var tenantId = TestData.NextTenant();
        await using var context = _fixture.CreateAdminContext();

        var draft = await new CreatePipelineDraftHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(
            Draft(tenantId, Guid.NewGuid(), null, 0, 0, "custom-labels", [
                new("Entry", 1, true, true),
                new("Kaybedildi", 99, false, true, Kind: PipelineStageKind.Lost),
                new("Kazanıldı", 98, false, true, Kind: PipelineStageKind.Won)]));

        await using var read = _fixture.CreateAdminContext();
        var stages = await read.PipelineStages.Where(s => s.TenantId == tenantId && s.PipelineDefinitionVersionId == draft.VersionId).OrderBy(s => s.SortOrder).ToListAsync();
        Assert.Equal(["Entry", "Kazanıldı", "Kaybedildi"], stages.Select(s => s.Name));
        Assert.Equal([PipelineStageKind.Open, PipelineStageKind.Won, PipelineStageKind.Lost], stages.Select(s => s.Kind));
        Assert.All(stages.Where(s => s.Kind != PipelineStageKind.Open), s => Assert.False(s.IsEntry));
    }

    [Fact]
    public async Task A_draft_loaded_with_its_system_stages_can_be_saved_again()
    {
        var tenantId = TestData.NextTenant();
        await using var context = _fixture.CreateAdminContext();
        var handler = new CreatePipelineDraftHandler(context, StubAuthorizer.AlwaysAllow);
        IReadOnlyList<PipelineStageInput> stages = [
            new("Entry", 1, true, true),
            new("Kazanıldı", 2, false, true, Kind: PipelineStageKind.Won),
            new("Kaybedildi", 3, false, true, Kind: PipelineStageKind.Lost)];

        var first = await handler.HandleAsync(Draft(tenantId, Guid.NewGuid(), null, 0, 0, "round-trip-1", stages));
        var second = await handler.HandleAsync(Draft(tenantId, Guid.NewGuid(), first.PipelineDefinitionId, first.RowVersion, first.VersionNumber, "round-trip-2", stages));

        Assert.Equal(2, second.VersionNumber);
    }

    [Fact]
    public async Task Validate_rejects_a_stage_that_reuses_a_system_stage_label_or_repeats_a_system_kind()
    {
        var tenantId = TestData.NextTenant();
        await using var context = _fixture.CreateAdminContext();
        var handler = new CreatePipelineDraftHandler(context, StubAuthorizer.AlwaysAllow);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(Draft(tenantId, Guid.NewGuid(), null, 0, 0, "dup-label", [
            new("Entry", 1, true, true), new("Kazanıldı", 2, false, true), new("Kazanıldı", 3, false, true, Kind: PipelineStageKind.Won)])));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(Draft(tenantId, Guid.NewGuid(), null, 0, 0, "two-won", [
            new("Entry", 1, true, true), new("A", 2, false, true, Kind: PipelineStageKind.Won), new("B", 3, false, true, Kind: PipelineStageKind.Won)])));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(Draft(tenantId, Guid.NewGuid(), null, 0, 0, "entry-won", [
            new("Entry", 1, true, true), new("A", 2, true, true, Kind: PipelineStageKind.Won)])));
    }

    [Fact]
    public void Validate_rejects_a_version_missing_a_lost_stage()
    {
        var tenant = TestData.NextTenant();
        var definition = PipelineDefinition.Create(tenant, "Sales");
        var version = definition.AddVersion(1);
        var entry = version.AddStage("Open", 10);
        var won = version.AddWonStage("Won", 990);
        var stages = new List<PipelineStage> { entry, won };

        var errors = PublishPipelineVersionHandler.Validate(version, stages, transitions: []);

        Assert.Contains(errors, e => e.Contains("Lost", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_accepts_a_correctly_shaped_version()
    {
        var tenant = TestData.NextTenant();
        var definition = PipelineDefinition.Create(tenant, "Sales");
        var version = definition.AddVersion(1);
        var entry = version.AddStage("Open", 10);
        var won = version.AddWonStage("Won", 990);
        var lost = version.AddLostStage("Lost", 1000);
        var stages = new List<PipelineStage> { entry, won, lost };

        var errors = PublishPipelineVersionHandler.Validate(version, stages, transitions: []);

        Assert.Empty(errors);
    }

    private CreatePipelineDraftCommand Draft(TenantId tenant, Guid correlationId, long? pipelineId, long expectedRowVersion,
        int expectedLatest, string key, IReadOnlyList<PipelineStageInput> stages,
        IReadOnlyList<PipelineTransitionInput>? transitions = null) => new(tenant, Administrator, pipelineId,
        "Sales", expectedRowVersion, expectedLatest, stages, transitions is { Count: > 0 }, transitions ?? [], key, correlationId);
}
