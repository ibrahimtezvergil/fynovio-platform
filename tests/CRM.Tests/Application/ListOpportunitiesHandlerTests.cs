using Contracts;
using CRM.Application;
using CRM.Domain;
using CRM.Tests;
using CRM.Tests.Integration;
using Xunit;

namespace CRM.Tests.Application;

[Collection(nameof(PostgresCollection))]
public sealed class ListOpportunitiesHandlerTests
{
    private readonly PostgresFixture _fixture;

    public ListOpportunitiesHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task All_scope_returns_every_opportunity_in_the_tenant()
    {
        var tenant = await SeedTwoOpportunitiesAsync();
        await using var context = _fixture.CreateAdminContext();

        var results = await new ListOpportunitiesHandler(context, new StubScopeResolver(new AccessScope.All()), StubDefinitionReader.None, StubLinkTargetDirectory.None)
            .HandleAsync(new ListOpportunitiesQuery(tenant, TestData.Seller, Guid.NewGuid(), Status: null, Skip: 0, Take: 50));

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task None_scope_returns_nothing()
    {
        var tenant = await SeedTwoOpportunitiesAsync();
        await using var context = _fixture.CreateAdminContext();

        var results = await new ListOpportunitiesHandler(context, new StubScopeResolver(new AccessScope.None()), StubDefinitionReader.None, StubLinkTargetDirectory.None)
            .HandleAsync(new ListOpportunitiesQuery(tenant, TestData.Seller, Guid.NewGuid(), Status: null, Skip: 0, Take: 50));

        Assert.Empty(results);
    }

    [Fact]
    public async Task OwnedBy_scope_returns_only_that_principals_opportunities()
    {
        var tenant = TestData.NextTenant();
        var other = new PrincipalRef("https://idp.local", "seller-other");
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");
        seed.Opportunities.Add(Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 100m));
        seed.Opportunities.Add(Opportunity.Create(tenant, partyRef, other, "TRY", 200m));
        await seed.SaveChangesAsync();

        await using var context = _fixture.CreateAdminContext();
        var scope = new AccessScope.AnyOf([new ScopeTerm.OwnedBy(TestData.Seller)]);
        var results = await new ListOpportunitiesHandler(context, new StubScopeResolver(scope), StubDefinitionReader.None, StubLinkTargetDirectory.None)
            .HandleAsync(new ListOpportunitiesQuery(tenant, TestData.Seller, Guid.NewGuid(), Status: null, Skip: 0, Take: 50));

        Assert.Single(results);
        Assert.Equal(TestData.Seller.Subject, results[0].AssignedPrincipalSubject);
    }

    [Fact]
    public async Task Summary_carries_the_party_the_pipeline_version_and_the_expiry_date_the_list_page_shows()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");
        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 100m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        await using var context = _fixture.CreateAdminContext();
        var results = await new ListOpportunitiesHandler(context, new StubScopeResolver(new AccessScope.All()), StubDefinitionReader.None, StubLinkTargetDirectory.None)
            .HandleAsync(new ListOpportunitiesQuery(tenant, TestData.Seller, Guid.NewGuid(), Status: null, Skip: 0, Take: 50));

        var summary = Assert.Single(results);
        Assert.Equal(partyRef.PartyId, summary.PartyId);
        Assert.Null(summary.PipelineDefinitionVersionId); // a Draft has no pipeline version until it is opened
        Assert.Null(summary.ExpiryDate);
    }

    private async Task<TenantId> SeedTwoOpportunitiesAsync()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");
        seed.Opportunities.Add(Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 100m));
        seed.Opportunities.Add(Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 200m));
        await seed.SaveChangesAsync();
        return tenant;
    }
}
