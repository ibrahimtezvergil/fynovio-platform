using Contracts;
using CRM.Application;
using CRM.Domain;
using CRM.Tests;
using CRM.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Application;

[Collection(nameof(PostgresCollection))]
public sealed class CreateOpportunityHandlerTests
{
    private readonly PostgresFixture _fixture;

    public CreateOpportunityHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Creating_persists_a_draft_opportunity_with_evidence_and_outbox()
    {
        var tenant = TestData.NextTenant();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");

        var command = new CreateOpportunityCommand(tenant, partyRef, TestData.Seller, "TRY", 1000m, "key-1", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        var result = await new CreateOpportunityHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        Assert.False(result.Replayed);
        Assert.True(result.OpportunityId > 0);

        var opportunity = await context.Opportunities.AsNoTracking().SingleAsync(o => o.Id == result.OpportunityId);
        Assert.Equal(OpportunityStatus.Draft, opportunity.Status);
        Assert.Single(await context.OutboxMessages.AsNoTracking().Where(m => m.AggregateId == result.OpportunityId).ToListAsync());
        Assert.Single(await context.EvidenceRecords.AsNoTracking().Where(e => e.AggregateId == result.OpportunityId).ToListAsync());
    }

    [Fact]
    public async Task Creating_is_denied_without_a_grant_for_the_action()
    {
        var tenant = TestData.NextTenant();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");
        var command = new CreateOpportunityCommand(tenant, partyRef, TestData.Seller, "TRY", 1000m, "key-denied", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<OpportunityAuthorizationDeniedException>(() =>
            new CreateOpportunityHandler(context, StubAuthorizer.AlwaysDeny).HandleAsync(command));

        Assert.Empty(await context.Opportunities.AsNoTracking().Where(o => o.PartyRefPartyId == partyRef.PartyId).ToListAsync());
    }

    [Fact]
    public async Task Retrying_with_the_same_key_replays_the_same_opportunity_id()
    {
        var tenant = TestData.NextTenant();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");
        var command = new CreateOpportunityCommand(tenant, partyRef, TestData.Seller, "TRY", 1000m, "key-2", Guid.NewGuid());

        await using var first = _fixture.CreateAdminContext();
        var firstResult = await new CreateOpportunityHandler(first, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        await using var second = _fixture.CreateAdminContext();
        var replay = await new CreateOpportunityHandler(second, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        Assert.True(replay.Replayed);
        Assert.Equal(firstResult.OpportunityId, replay.OpportunityId);
    }
}
