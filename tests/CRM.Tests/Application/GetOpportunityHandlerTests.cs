using Contracts;
using CRM.Application;
using CRM.Domain;
using CRM.Tests.Integration;
using Xunit;

namespace CRM.Tests.Application;

[Collection(nameof(PostgresCollection))]
public sealed class GetOpportunityHandlerTests
{
    private readonly PostgresFixture _fixture;

    public GetOpportunityHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Returns_the_opportunity_when_authorized()
    {
        var (tenant, opportunityId) = await SeedAsync();
        await using var context = _fixture.CreateAdminContext();

        var dto = await new GetOpportunityHandler(context, StubAuthorizer.AlwaysAllow)
            .HandleAsync(new GetOpportunityQuery(tenant, opportunityId, TestData.Seller, Guid.NewGuid()));

        Assert.NotNull(dto);
        Assert.Equal(opportunityId, dto!.Id);
    }

    [Fact]
    public async Task Returns_null_when_denied_indistinguishable_from_not_found()
    {
        var (tenant, opportunityId) = await SeedAsync();
        await using var context = _fixture.CreateAdminContext();

        var dto = await new GetOpportunityHandler(context, StubAuthorizer.AlwaysDeny)
            .HandleAsync(new GetOpportunityQuery(tenant, opportunityId, TestData.Seller, Guid.NewGuid()));

        Assert.Null(dto);
    }

    [Fact]
    public async Task Returns_null_for_a_genuinely_missing_opportunity()
    {
        var tenant = TestData.NextTenant();
        await using var context = _fixture.CreateAdminContext();

        var dto = await new GetOpportunityHandler(context, StubAuthorizer.AlwaysAllow)
            .HandleAsync(new GetOpportunityQuery(tenant, 999_999, TestData.Seller, Guid.NewGuid()));

        Assert.Null(dto);
    }

    private async Task<(TenantId TenantId, long OpportunityId)> SeedAsync()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");
        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();
        return (tenant, opportunity.Id);
    }
}
