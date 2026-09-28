using Contracts;
using CRM.Application;
using CRM.Domain;
using CRM.Tests;
using CRM.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Application;

[Collection(nameof(PostgresCollection))]
public sealed class WinOpportunityHandlerTests
{
    private readonly PostgresFixture _fixture;

    public WinOpportunityHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task HandleAsync_moves_the_opportunity_onto_the_pipelines_won_stage()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");

        // Seed a published pipeline with Won and Lost stages (as Task 10 guarantees)
        var definition = PipelineDefinition.Create(tenant, "Sales");
        seed.PipelineDefinitions.Add(definition);
        await seed.SaveChangesAsync();
        var version = definition.AddVersion(1);
        seed.PipelineDefinitionVersions.Add(version);
        await seed.SaveChangesAsync();
        var entryStage = version.AddStage("Bekliyor", 0);
        seed.PipelineStages.Add(entryStage);
        var wonStage = version.AddWonStage("Kazanıldı", 1);
        seed.PipelineStages.Add(wonStage);
        var lostStage = version.AddLostStage("Kaybedildi", 2);
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

        var command = new WinOpportunityCommand(
            tenant, opportunity.Id, TestData.Seller, reloaded.RowVersion, "key-win", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        var result = await new WinOpportunityHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        Assert.False(result.Replayed);
        var reloadedAfterWin = await context.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunity.Id);
        Assert.Equal(wonStage.Id, reloadedAfterWin.PipelineStageId);
        Assert.Equal(entryStage.Id, reloadedAfterWin.ClosedFromStageId);
    }

    [Fact]
    public async Task Winning_persists_the_total_and_writes_evidence_and_outbox()
    {
        var (tenant, opportunityId, version) = await SeedOpenOpportunityAsync();
        var command = new WinOpportunityCommand(tenant, opportunityId, TestData.Seller, version, "key-2", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        var result = await new WinOpportunityHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        Assert.False(result.Replayed);
        var reloaded = await context.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunityId);
        Assert.Equal(OpportunityStatus.Won, reloaded.Status);
        Assert.NotNull(reloaded.TotalAmount);
        var wonOutbox = await context.OutboxMessages.AsNoTracking()
            .Where(m => m.AggregateId == opportunityId && m.EventType == "enterprise.crmsales.opportunity.won.v1")
            .ToListAsync();
        Assert.Single(wonOutbox);
        var wonEvidence = await context.EvidenceRecords.AsNoTracking()
            .Where(e => e.AggregateId == opportunityId && e.Action == "Opportunity.Win")
            .ToListAsync();
        Assert.Single(wonEvidence);
    }

    [Fact]
    public async Task Winning_is_denied_without_a_grant_for_the_action()
    {
        var (tenant, opportunityId, version) = await SeedOpenOpportunityAsync();
        var command = new WinOpportunityCommand(tenant, opportunityId, TestData.Seller, version, "key-3", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<OpportunityAuthorizationDeniedException>(() =>
            new WinOpportunityHandler(context, StubAuthorizer.AlwaysDeny).HandleAsync(command));
    }

    [Fact]
    public async Task Retrying_with_the_same_key_replays_without_reapplying_the_win()
    {
        var (tenant, opportunityId, version) = await SeedOpenOpportunityAsync();
        var command = new WinOpportunityCommand(tenant, opportunityId, TestData.Seller, version, "key-replay", Guid.NewGuid());

        await using (var first = _fixture.CreateAdminContext())
        {
            var firstResult = await new WinOpportunityHandler(first, StubAuthorizer.AlwaysAllow).HandleAsync(command);
            Assert.False(firstResult.Replayed);
        }

        await using var second = _fixture.CreateAdminContext();
        var replay = await new WinOpportunityHandler(second, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        Assert.True(replay.Replayed);
        Assert.Equal(opportunityId, replay.OpportunityId);

        await using var verification = _fixture.CreateAdminContext();
        var wonOutbox = await verification.OutboxMessages.AsNoTracking()
            .Where(m => m.AggregateId == opportunityId && m.EventType == "enterprise.crmsales.opportunity.won.v1")
            .ToListAsync();
        Assert.Single(wonOutbox);
        var wonEvidence = await verification.EvidenceRecords.AsNoTracking()
            .Where(e => e.AggregateId == opportunityId && e.Action == "Opportunity.Win")
            .ToListAsync();
        Assert.Single(wonEvidence);
    }

    private async Task<(TenantId TenantId, long OpportunityId, long RowVersion)> SeedOpenOpportunityAsync()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");

        // Seed a published pipeline with Won stage
        var definition = PipelineDefinition.Create(tenant, "Sales");
        seed.PipelineDefinitions.Add(definition);
        await seed.SaveChangesAsync();
        var version = definition.AddVersion(1);
        seed.PipelineDefinitionVersions.Add(version);
        await seed.SaveChangesAsync();
        var entryStage = version.AddStage("Bekliyor", 0);
        seed.PipelineStages.Add(entryStage);
        var wonStage = version.AddWonStage("Kazanıldı", 1);
        seed.PipelineStages.Add(wonStage);
        version.Publish();
        await seed.SaveChangesAsync();

        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        opportunity.AddLine(TestData.ProductRef(tenant), 1, 1000m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        var openCommand = new OpenOpportunityCommand(
            tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion,
            DateTimeOffset.UtcNow.AddDays(7), "open-key", Guid.NewGuid());

        await using var openContext = _fixture.CreateAdminContext();
        await new OpenOpportunityHandler(openContext, StubAuthorizer.AlwaysAllow).HandleAsync(openCommand);

        await using var reload = _fixture.CreateAdminContext();
        var reloaded = await reload.Opportunities.SingleAsync(o => o.Id == opportunity.Id);
        return (tenant, opportunity.Id, reloaded.RowVersion);
    }
}
