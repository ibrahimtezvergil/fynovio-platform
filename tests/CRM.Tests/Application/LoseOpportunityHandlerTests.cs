using Contracts;
using CRM.Application;
using CRM.Domain;
using CRM.Tests;
using CRM.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Application;

[Collection(nameof(PostgresCollection))]
public sealed class LoseOpportunityHandlerTests
{
    private readonly PostgresFixture _fixture;

    public LoseOpportunityHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task HandleAsync_moves_the_opportunity_onto_the_pipelines_lost_stage()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");

        // Seed a published pipeline with Lost stage (as Task 10 guarantees)
        var definition = PipelineDefinition.Create(tenant, "Sales");
        seed.PipelineDefinitions.Add(definition);
        await seed.SaveChangesAsync();
        var version = definition.AddVersion(1);
        seed.PipelineDefinitionVersions.Add(version);
        await seed.SaveChangesAsync();
        var entryStage = version.AddStage("Bekliyor", 0);
        seed.PipelineStages.Add(entryStage);
        var lostStage = version.AddLostStage("Kaybedildi", 1);
        seed.PipelineStages.Add(lostStage);
        version.Publish();
        await seed.SaveChangesAsync();

        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        opportunity.AddLine(TestData.ProductRef(tenant), 1, 1000m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        var openCommand = new OpenOpportunityCommand(
            tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion,
            DateTimeOffset.UtcNow.AddDays(7), "open-key", Guid.NewGuid());

        await using (var openContext = _fixture.CreateAdminContext())
            await new OpenOpportunityHandler(openContext, StubAuthorizer.AlwaysAllow).HandleAsync(openCommand);

        await using var reload = _fixture.CreateAdminContext();
        var reloaded = await reload.Opportunities.SingleAsync(o => o.Id == opportunity.Id);

        var command = new LoseOpportunityCommand(tenant, opportunity.Id, TestData.Seller, reloaded.RowVersion, "Fiyat rekabetçi değildi", "key-lose-stage", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        var result = await new LoseOpportunityHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        Assert.False(result.Replayed);
        var reloadedLost = await context.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunity.Id);
        Assert.Equal(lostStage.Id, reloadedLost.PipelineStageId);
        Assert.Equal(entryStage.Id, reloadedLost.ClosedFromStageId);
    }

    [Fact]
    public async Task HandleAsync_loses_a_draft_opportunity_without_assigning_a_stage()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");
        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        var command = new LoseOpportunityCommand(tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion, "hiç açılmadı", "key-draft-lose", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        var result = await new LoseOpportunityHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        Assert.False(result.Replayed);
        var reloaded = await context.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunity.Id);
        Assert.Equal(OpportunityStatus.Lost, reloaded.Status);
        Assert.Null(reloaded.PipelineStageId);
        Assert.Null(reloaded.ClosedFromStageId);
    }

    [Fact]
    public async Task Losing_persists_the_reason_and_writes_evidence_and_outbox()
    {
        var (tenant, opportunityId, version) = await SeedOpenOpportunityAsync();
        var command = new LoseOpportunityCommand(tenant, opportunityId, TestData.Seller, version, "Fiyat rekabetçi değildi", "key-1", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        var result = await new LoseOpportunityHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        Assert.False(result.Replayed);
        var reloaded = await context.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunityId);
        Assert.Equal(OpportunityStatus.Lost, reloaded.Status);
        Assert.Equal("Fiyat rekabetçi değildi", reloaded.LostReason);
        Assert.Single(await context.OutboxMessages.AsNoTracking().Where(m => m.AggregateId == opportunityId).ToListAsync());
        Assert.Single(await context.EvidenceRecords.AsNoTracking().Where(e => e.AggregateId == opportunityId).ToListAsync());
    }

    [Fact]
    public async Task Losing_is_denied_without_a_grant_for_the_action()
    {
        var (tenant, opportunityId, version) = await SeedOpenOpportunityAsync();
        var command = new LoseOpportunityCommand(tenant, opportunityId, TestData.Seller, version, "reason", "key-2", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<OpportunityAuthorizationDeniedException>(() =>
            new LoseOpportunityHandler(context, StubAuthorizer.AlwaysDeny).HandleAsync(command));
    }

    [Fact]
    public async Task Retrying_with_the_same_key_replays_without_reapplying_the_loss()
    {
        var (tenant, opportunityId, version) = await SeedOpenOpportunityAsync();
        var command = new LoseOpportunityCommand(tenant, opportunityId, TestData.Seller, version, "reason", "key-replay", Guid.NewGuid());

        await using (var first = _fixture.CreateAdminContext())
        {
            var firstResult = await new LoseOpportunityHandler(first, StubAuthorizer.AlwaysAllow).HandleAsync(command);
            Assert.False(firstResult.Replayed);
        }

        await using var second = _fixture.CreateAdminContext();
        var replay = await new LoseOpportunityHandler(second, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        Assert.True(replay.Replayed);
        Assert.Equal(opportunityId, replay.OpportunityId);

        await using var verification = _fixture.CreateAdminContext();
        Assert.Single(await verification.OutboxMessages.AsNoTracking().Where(m => m.AggregateId == opportunityId).ToListAsync());
        Assert.Single(await verification.EvidenceRecords.AsNoTracking().Where(e => e.AggregateId == opportunityId).ToListAsync());
    }

    private async Task<(TenantId TenantId, long OpportunityId, long RowVersion)> SeedOpenOpportunityAsync()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");
        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        opportunity.AddLine(TestData.ProductRef(tenant), 1, 1000m);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: null, pipelineStageId: null);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();
        return (tenant, opportunity.Id, opportunity.RowVersion);
    }

    [Fact]
    public async Task HandleAsync_closes_the_open_stage_history_entry_and_opens_a_lost_entry()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");

        var definition = PipelineDefinition.Create(tenant, "Sales");
        seed.PipelineDefinitions.Add(definition);
        await seed.SaveChangesAsync();
        var version = definition.AddVersion(1);
        seed.PipelineDefinitionVersions.Add(version);
        await seed.SaveChangesAsync();
        var entryStage = version.AddStage("Bekliyor", 0);
        seed.PipelineStages.Add(entryStage);
        var lostStage = version.AddLostStage("Kaybedildi", 1);
        seed.PipelineStages.Add(lostStage);
        version.Publish();
        await seed.SaveChangesAsync();

        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        opportunity.AddLine(TestData.ProductRef(tenant), 1, 1000m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        var openCommand = new OpenOpportunityCommand(
            tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion,
            DateTimeOffset.UtcNow.AddDays(7), "open-key-history", Guid.NewGuid());
        await using (var openContext = _fixture.CreateAdminContext())
            await new OpenOpportunityHandler(openContext, StubAuthorizer.AlwaysAllow).HandleAsync(openCommand);

        await using var reload = _fixture.CreateAdminContext();
        var reloaded = await reload.Opportunities.SingleAsync(o => o.Id == opportunity.Id);

        var command = new LoseOpportunityCommand(tenant, opportunity.Id, TestData.Seller, reloaded.RowVersion, "Lost reason", "key-lose-history", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        await new LoseOpportunityHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        var entries = await context.OpportunityStageHistory
            .Where(h => h.OpportunityId == opportunity.Id)
            .OrderBy(h => h.Id)
            .ToListAsync();
        Assert.Equal(2, entries.Count);
        Assert.Equal(entryStage.Id, entries[0].PipelineStageId);
        Assert.NotNull(entries[0].ExitedAt);
        Assert.Equal(lostStage.Id, entries[1].PipelineStageId);
        Assert.Null(entries[1].ExitedAt);
    }
}
