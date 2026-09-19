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
}
