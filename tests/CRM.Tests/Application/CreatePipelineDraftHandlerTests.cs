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
