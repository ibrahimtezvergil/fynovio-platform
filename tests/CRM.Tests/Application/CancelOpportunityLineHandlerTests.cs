using Contracts;
using CRM.Application;
using CRM.Domain;
using CRM.Tests;
using CRM.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Application;

[Collection(nameof(PostgresCollection))]
public sealed class CancelOpportunityLineHandlerTests
{
    private readonly PostgresFixture _fixture;

    public CancelOpportunityLineHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Canceling_a_line_persists_the_cancellation_and_writes_evidence()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");
        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        var line = opportunity.AddLine(TestData.ProductRef(tenant), 1, 1000m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        var command = new CancelOpportunityLineCommand(
            tenant, opportunity.Id, line.Id, TestData.Seller, opportunity.RowVersion,
            "no longer needed", "key-1", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        var result = await new CancelOpportunityLineHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        Assert.False(result.Replayed);
        var reloaded = await context.Opportunities.Include(o => o.Lines).AsNoTracking().SingleAsync(o => o.Id == opportunity.Id);
        Assert.True(reloaded.Lines.Single().IsCanceled);
    }
}
