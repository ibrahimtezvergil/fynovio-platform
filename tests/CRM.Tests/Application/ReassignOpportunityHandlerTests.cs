using Contracts;
using CRM.Application;
using CRM.Domain;
using CRM.Tests;
using CRM.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Application;

[Collection(nameof(PostgresCollection))]
public sealed class ReassignOpportunityHandlerTests
{
    private readonly PostgresFixture _fixture;

    public ReassignOpportunityHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Reassigning_persists_the_new_owner_and_writes_evidence_and_outbox()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");
        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        var newOwner = new PrincipalRef("https://idp.local", "seller-2");
        var command = new ReassignOpportunityCommand(tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion, newOwner, "key-1", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        var result = await new ReassignOpportunityHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        Assert.False(result.Replayed);
        var reloaded = await context.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunity.Id);
        Assert.Equal(newOwner, reloaded.AssignedPrincipal);
        Assert.Single(await context.OutboxMessages.AsNoTracking().Where(m => m.AggregateId == opportunity.Id).ToListAsync());
        Assert.Single(await context.EvidenceRecords.AsNoTracking().Where(e => e.AggregateId == opportunity.Id).ToListAsync());
    }

    [Fact]
    public async Task Reassigning_is_denied_without_a_grant_for_the_action()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");
        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        var newOwner = new PrincipalRef("https://idp.local", "seller-2");
        var command = new ReassignOpportunityCommand(tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion, newOwner, "key-denied", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<OpportunityAuthorizationDeniedException>(() =>
            new ReassignOpportunityHandler(context, StubAuthorizer.AlwaysDeny).HandleAsync(command));

        var untouched = await context.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunity.Id);
        Assert.Equal(TestData.Seller, untouched.AssignedPrincipal);
    }

    [Fact]
    public async Task Retrying_with_the_same_key_replays_without_reapplying_the_reassignment()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");
        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        var newOwner = new PrincipalRef("https://idp.local", "seller-2");
        var command = new ReassignOpportunityCommand(tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion, newOwner, "key-replay", Guid.NewGuid());

        await using (var first = _fixture.CreateAdminContext())
        {
            var firstResult = await new ReassignOpportunityHandler(first, StubAuthorizer.AlwaysAllow).HandleAsync(command);
            Assert.False(firstResult.Replayed);
        }

        await using var second = _fixture.CreateAdminContext();
        var replay = await new ReassignOpportunityHandler(second, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        Assert.True(replay.Replayed);
        Assert.Equal(opportunity.Id, replay.OpportunityId);

        await using var verification = _fixture.CreateAdminContext();
        Assert.Single(await verification.OutboxMessages.AsNoTracking().Where(m => m.AggregateId == opportunity.Id).ToListAsync());
        Assert.Single(await verification.EvidenceRecords.AsNoTracking().Where(e => e.AggregateId == opportunity.Id).ToListAsync());
    }
}
