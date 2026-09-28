using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class OpportunityPersistenceTests
{
    private readonly PostgresFixture _fixture;

    public OpportunityPersistenceTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Losing_a_draft_opportunity_persists()
    {
        await using var crmContext = _fixture.CreateAdminContext();
        await using var masterDataContext = _fixture.CreateMasterDataContext();
        var tenant = TestData.NextTenant();

        var partyRef = await TestData.CreatePartyAsync(masterDataContext, tenant, "Acme");

        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        crmContext.Opportunities.Add(opportunity);
        await crmContext.SaveChangesAsync();

        opportunity.Lose("müşteri vazgeçti");
        await crmContext.SaveChangesAsync();

        var reloaded = await crmContext.Opportunities.AsNoTracking()
            .SingleAsync(o => o.Id == opportunity.Id);
        Assert.Equal(OpportunityStatus.Lost, reloaded.Status);
    }

    [Fact]
    public async Task Losing_an_open_opportunity_persists()
    {
        await using var crmContext = _fixture.CreateAdminContext();
        await using var masterDataContext = _fixture.CreateMasterDataContext();
        var tenant = TestData.NextTenant();

        var partyRef = await TestData.CreatePartyAsync(masterDataContext, tenant, "Acme");

        var (versionId, stageId) = await TestData.CreatePublishedPipelineWithEntryStageAsync(crmContext, tenant);
        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), versionId, stageId);
        crmContext.Opportunities.Add(opportunity);
        await crmContext.SaveChangesAsync();

        opportunity.Lose("bütçe onaylanmadı");
        await crmContext.SaveChangesAsync();

        var reloaded = await crmContext.Opportunities.AsNoTracking()
            .SingleAsync(o => o.Id == opportunity.Id);
        Assert.Equal(OpportunityStatus.Lost, reloaded.Status);
    }

    [Fact]
    public async Task Line_total_and_won_total_are_persisted()
    {
        await using var crmContext = _fixture.CreateAdminContext();
        await using var masterDataContext = _fixture.CreateMasterDataContext();
        var tenant = TestData.NextTenant();

        var partyRef = await TestData.CreatePartyAsync(masterDataContext, tenant, "Acme");

        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 0m);
        opportunity.AddLine(TestData.ProductRef(tenant), quantity: 3, unitPrice: 33.33m);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: null, pipelineStageId: null);
        opportunity.Win();
        crmContext.Opportunities.Add(opportunity);
        await crmContext.SaveChangesAsync();

        await using var verification = _fixture.CreateAdminContext();
        var reloaded = await verification.Opportunities.AsNoTracking()
            .Include(o => o.Lines)
            .SingleAsync(o => o.Id == opportunity.Id);

        Assert.Equal(99.99m, reloaded.TotalAmount);
        Assert.Equal(99.99m, reloaded.Lines.Single().LineTotal);
    }
}
