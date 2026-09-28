using Contracts;
using CRM.Application;
using CRM.Domain;
using CRM.Tests;
using CRM.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Application;

[Collection(nameof(PostgresCollection))]
public sealed class MoveOpportunityToPipelineHandlerTests
{
    private readonly PostgresFixture _fixture;

    public MoveOpportunityToPipelineHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Moving_to_a_different_published_pipeline_with_an_active_open_stage_succeeds()
    {
        var (tenant, opportunityId, targetVersionId, targetStageId, _, _) = await SeedAsync();
        var opportunity = await LoadAsync(tenant, opportunityId);
        var command = new MoveOpportunityToPipelineCommand(
            tenant, opportunityId, TestData.Seller, opportunity.RowVersion, targetVersionId, targetStageId, "key-move-1", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        var result = await new MoveOpportunityToPipelineHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        Assert.False(result.Replayed);
        var reloaded = await context.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunityId);
        Assert.Equal(targetVersionId, reloaded.PipelineDefinitionVersionId);
        Assert.Equal(targetStageId, reloaded.PipelineStageId);
    }

    [Fact]
    public async Task Moving_to_an_unpublished_pipeline_is_rejected()
    {
        var (tenant, opportunityId, _, _, unpublishedVersionId, _) = await SeedAsync();
        var opportunity = await LoadAsync(tenant, opportunityId);

        await using var seed = _fixture.CreateAdminContext();
        var unpublishedVersion = await seed.PipelineDefinitionVersions.AsNoTracking()
            .SingleAsync(v => v.Id == unpublishedVersionId);
        var unpublishedStage = unpublishedVersion.AddStage("Aşama", 0);
        seed.PipelineStages.Add(unpublishedStage);
        await seed.SaveChangesAsync();

        var command = new MoveOpportunityToPipelineCommand(
            tenant, opportunityId, TestData.Seller, opportunity.RowVersion, unpublishedVersionId, unpublishedStage.Id, "key-unpub", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<InvalidPipelineTransitionException>(() =>
            new MoveOpportunityToPipelineHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command));
    }

    [Fact]
    public async Task Moving_to_an_inactive_stage_in_the_target_pipeline_is_rejected()
    {
        var (tenant, opportunityId, targetVersionId, targetStageId, _, _) = await SeedAsync();
        var opportunity = await LoadAsync(tenant, opportunityId);

        await using var seed = _fixture.CreateAdminContext();
        // Create a new inactive stage using Create then Deactivate
        var inactiveStage = PipelineStage.Create(tenant, targetVersionId, "Inactive", 10, false);
        inactiveStage.Deactivate();
        seed.PipelineStages.Add(inactiveStage);
        await seed.SaveChangesAsync();

        var command = new MoveOpportunityToPipelineCommand(
            tenant, opportunityId, TestData.Seller, opportunity.RowVersion, targetVersionId, inactiveStage.Id, "key-inactive", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<InvalidPipelineTransitionException>(() =>
            new MoveOpportunityToPipelineHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command));
    }

    [Fact]
    public async Task Moving_to_a_won_stage_is_rejected()
    {
        var (tenant, opportunityId, targetVersionId, _, _, _) = await SeedAsync();
        var opportunity = await LoadAsync(tenant, opportunityId);

        await using var seed = _fixture.CreateAdminContext();
        var wonStage = PipelineStage.Create(tenant, targetVersionId, "Won", 10, false, PipelineStageKind.Won);
        seed.PipelineStages.Add(wonStage);
        await seed.SaveChangesAsync();

        var command = new MoveOpportunityToPipelineCommand(
            tenant, opportunityId, TestData.Seller, opportunity.RowVersion, targetVersionId, wonStage.Id, "key-won", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<InvalidPipelineTransitionException>(() =>
            new MoveOpportunityToPipelineHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command));
    }

    [Fact]
    public async Task Moving_a_closed_opportunity_is_rejected()
    {
        var (tenant, opportunityId, targetVersionId, targetStageId, _, _) = await SeedAsync();
        var opportunity = await LoadAsync(tenant, opportunityId);

        // Lose the opportunity first
        await using var seed = _fixture.CreateAdminContext();
        var opp = await seed.Opportunities.SingleAsync(o => o.Id == opportunityId);
        opp.Lose("test reason");
        await seed.SaveChangesAsync();

        var command = new MoveOpportunityToPipelineCommand(
            tenant, opportunityId, TestData.Seller, opportunity.RowVersion + 1, targetVersionId, targetStageId, "key-closed", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new MoveOpportunityToPipelineHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command));
    }

    [Fact]
    public async Task Moving_to_a_stage_that_belongs_to_a_different_pipeline_version_is_rejected()
    {
        var (tenant, opportunityId, targetVersionId, _, _, sourceStageId) = await SeedAsync();
        var opportunity = await LoadAsync(tenant, opportunityId);

        // sourceStageId belongs to the source pipeline version, not targetVersionId.
        var command = new MoveOpportunityToPipelineCommand(
            tenant, opportunityId, TestData.Seller, opportunity.RowVersion, targetVersionId, sourceStageId, "key-wrong-version", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<InvalidPipelineTransitionException>(() =>
            new MoveOpportunityToPipelineHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command));
    }

    private async Task<(TenantId TenantId, long OpportunityId, long TargetVersionId, long TargetStageId, long UnpublishedVersionId, long SourceStageId)> SeedAsync()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");

        // Create source pipeline (the one the opportunity starts in)
        var sourceDefinition = PipelineDefinition.Create(tenant, "Sales");
        seed.PipelineDefinitions.Add(sourceDefinition);
        await seed.SaveChangesAsync();
        var sourceVersion = sourceDefinition.AddVersion(1);
        seed.PipelineDefinitionVersions.Add(sourceVersion);
        await seed.SaveChangesAsync();
        var sourceStage = sourceVersion.AddStage("Bekliyor", 0);
        seed.PipelineStages.Add(sourceStage);
        await seed.SaveChangesAsync();
        // Publish source version
        sourceVersion.Publish();
        await seed.SaveChangesAsync();

        // Create target pipeline (the one we move to)
        var targetDefinition = PipelineDefinition.Create(tenant, "Support");
        seed.PipelineDefinitions.Add(targetDefinition);
        await seed.SaveChangesAsync();
        var targetVersion = targetDefinition.AddVersion(1);
        seed.PipelineDefinitionVersions.Add(targetVersion);
        await seed.SaveChangesAsync();
        var targetStage = targetVersion.AddStage("Teklif Verildi", 0);
        seed.PipelineStages.Add(targetStage);
        await seed.SaveChangesAsync();
        // Publish target version
        targetVersion.Publish();
        await seed.SaveChangesAsync();

        // Create unpublished version for validation testing
        var unpublishedVersion = targetDefinition.AddVersion(2);
        seed.PipelineDefinitionVersions.Add(unpublishedVersion);
        await seed.SaveChangesAsync();

        // Create opportunity in source pipeline
        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        opportunity.AddLine(TestData.ProductRef(tenant), 1, 1000m);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: sourceVersion.Id, pipelineStageId: sourceStage.Id);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        return (tenant, opportunity.Id, targetVersion.Id, targetStage.Id, unpublishedVersion.Id, sourceStage.Id);
    }

    private async Task<Opportunity> LoadAsync(TenantId tenant, long opportunityId)
    {
        await using var context = _fixture.CreateAdminContext();
        return await context.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunityId);
    }

    [Fact]
    public async Task Moving_closes_the_open_stage_history_entry_and_opens_a_new_one_for_target_stage()
    {
        var (tenant, opportunityId, targetVersionId, targetStageId, _, sourceStageId) = await SeedAsync();
        var opportunity = await LoadAsync(tenant, opportunityId);
        var command = new MoveOpportunityToPipelineCommand(
            tenant, opportunityId, TestData.Seller, opportunity.RowVersion, targetVersionId, targetStageId, "key-move-history", Guid.NewGuid());

        // Opportunity.Open() in SeedAsync was called directly, not through
        // OpenOpportunityHandler, so the "entered the source stage" history row it would
        // have written doesn't exist yet — seed it manually so the handler has something to close.
        await using (var seed = _fixture.CreateAdminContext())
        {
            seed.OpportunityStageHistory.Add(OpportunityStageHistoryEntry.Open(
                tenant, opportunityId, opportunity.PipelineDefinitionVersionId!.Value, sourceStageId));
            await seed.SaveChangesAsync();
        }

        await using var context = _fixture.CreateAdminContext();
        await new MoveOpportunityToPipelineHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        var entries = await context.OpportunityStageHistory
            .Where(h => h.OpportunityId == opportunityId)
            .OrderBy(h => h.Id)
            .ToListAsync();
        Assert.Equal(2, entries.Count);
        Assert.Equal(sourceStageId, entries[0].PipelineStageId);
        Assert.NotNull(entries[0].ExitedAt);
        Assert.Equal(targetStageId, entries[1].PipelineStageId);
        Assert.Null(entries[1].ExitedAt);
    }
}
