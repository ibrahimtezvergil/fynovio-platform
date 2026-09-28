using Contracts;
using CRM.Application;
using CRM.Domain;
using CRM.Tests;
using CRM.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Application;

[Collection(nameof(PostgresCollection))]
public sealed class AddOpportunityLineHandlerTests
{
    private readonly PostgresFixture _fixture;

    public AddOpportunityLineHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Adding_a_line_to_a_draft_opportunity_persists_it_and_writes_evidence()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");
        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        var command = new AddOpportunityLineCommand(
            tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion,
            TestData.ProductRef(tenant), 2, 500m, false, 0,
            "key-1", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        var result = await new AddOpportunityLineHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        Assert.False(result.Replayed);

        var reloaded = await context.Opportunities.Include(o => o.Lines).AsNoTracking().SingleAsync(o => o.Id == opportunity.Id);
        Assert.Single(reloaded.Lines);
        Assert.Single(await context.EvidenceRecords.AsNoTracking().Where(e => e.AggregateId == opportunity.Id).ToListAsync());
    }

    [Fact]
    public async Task Adding_a_line_to_an_opened_opportunity_is_rejected_by_the_domain_invariant()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");
        var (versionId, stageId) = await TestData.CreatePublishedPipelineWithEntryStageAsync(seed, tenant);
        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        opportunity.AddLine(TestData.ProductRef(tenant), 1, 1000m);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), versionId, stageId);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        var command = new AddOpportunityLineCommand(
            tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion,
            TestData.ProductRef(tenant), 1, 100m, false, 1, "key-2", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AddOpportunityLineHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command));
    }
}
