using Contracts;
using CRM.Application;
using CRM.Domain;
using CRM.Tests.Integration;
using Xunit;

namespace CRM.Tests.Application;

[Collection(nameof(PostgresCollection))]
public sealed class ListAssignablePrincipalsHandlerTests
{
    private static readonly PrincipalRef Rep = new("https://idp.local", "rep");
    private static readonly PrincipalRef Manager = new("https://idp.local", "manager");

    private readonly PostgresFixture _fixture;

    public ListAssignablePrincipalsHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<(TenantId Tenant, Opportunity Opportunity)> SeedAsync(Action<Opportunity>? configure = null)
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var masterData = _fixture.CreateMasterDataContext();
        var opportunity = Opportunity.Create(tenant, await TestData.CreatePartyAsync(masterData, tenant, "Acme"), TestData.Seller, "TRY", 1000m);
        configure?.Invoke(opportunity);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();
        return (tenant, opportunity);
    }

    private ListAssignablePrincipalsQuery Query(TenantId tenant, long id, string? search = null, int take = 10) =>
        new(tenant, id, TestData.Seller, search, take, Guid.NewGuid());

    [Fact]
    public async Task Returns_what_the_directory_permits_minus_the_current_owner()
    {
        var (tenant, opportunity) = await SeedAsync();
        var directory = StubPrincipalDirectory.Permitting(Rep, TestData.Seller, Manager);
        await using var context = _fixture.CreateAdminContext();

        var result = await new ListAssignablePrincipalsHandler(context, StubAuthorizer.AlwaysAllow, directory)
            .HandleAsync(Query(tenant, opportunity.Id, "ra", 7));

        Assert.Equal(["rep", "manager"], result.Select(r => r.Subject));
        Assert.All(result, r => Assert.Equal("https://idp.local", r.Issuer));
        Assert.Equal(("ra", 7), Assert.Single(directory.Listings));
        Assert.Equal(["crm.opportunity.read", "crm.opportunity.change_stage"], Assert.Single(directory.AskedActions).Select(a => a.Value));
    }

    [Fact]
    public async Task Is_gated_by_the_reassign_action_on_that_record()
    {
        var (tenant, opportunity) = await SeedAsync();
        var authorizer = new RecordingAuthorizer();
        await using var context = _fixture.CreateAdminContext();

        await new ListAssignablePrincipalsHandler(context, authorizer, StubPrincipalDirectory.Permitting(Rep)).HandleAsync(Query(tenant, opportunity.Id));

        Assert.Equal(["crm.opportunity.reassign"], authorizer.Actions);
    }

    [Theory]
    [InlineData(AuthorizationDenialStage.Coarse)]
    [InlineData(AuthorizationDenialStage.Record)]
    public async Task A_denied_actor_gets_the_denial_and_the_directory_is_never_consulted(AuthorizationDenialStage stage)
    {
        var (tenant, opportunity) = await SeedAsync();
        var directory = StubPrincipalDirectory.Permitting(Rep);
        var authorizer = stage == AuthorizationDenialStage.Coarse ? StubAuthorizer.AlwaysDeny : StubAuthorizer.RecordDenied;
        await using var context = _fixture.CreateAdminContext();

        var exception = await Assert.ThrowsAsync<OpportunityAuthorizationDeniedException>(() =>
            new ListAssignablePrincipalsHandler(context, authorizer, directory).HandleAsync(Query(tenant, opportunity.Id)));

        Assert.Equal(stage, exception.DenialStage);
        Assert.Equal(0, directory.Calls);
    }

    [Fact]
    public async Task An_unknown_opportunity_is_not_found()
    {
        var tenant = TestData.NextTenant();
        await using var context = _fixture.CreateAdminContext();

        await Assert.ThrowsAsync<OpportunityNotFoundException>(() =>
            new ListAssignablePrincipalsHandler(context, StubAuthorizer.AlwaysAllow, StubPrincipalDirectory.Permitting(Rep)).HandleAsync(Query(tenant, 999_999)));
    }

    [Fact]
    public async Task Another_tenants_opportunity_is_not_found()
    {
        var (_, opportunity) = await SeedAsync();
        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());

        await Assert.ThrowsAsync<OpportunityNotFoundException>(() =>
            new ListAssignablePrincipalsHandler(context, StubAuthorizer.AlwaysAllow, StubPrincipalDirectory.Permitting(Rep)).HandleAsync(Query(TestData.NextTenant(), opportunity.Id)));
    }

    [Fact]
    public async Task A_closed_opportunity_has_no_assignable_principals_and_the_directory_is_not_asked()
    {
        var (tenant, opportunity) = await SeedAsync(o => o.Lose("no budget"));
        var directory = StubPrincipalDirectory.Permitting(Rep);
        await using var context = _fixture.CreateAdminContext();

        var result = await new ListAssignablePrincipalsHandler(context, StubAuthorizer.AlwaysAllow, directory).HandleAsync(Query(tenant, opportunity.Id));

        Assert.Empty(result);
        Assert.Equal(0, directory.Calls);
    }

    [Fact]
    public async Task A_missing_take_falls_back_to_the_default_page_size()
    {
        var (tenant, opportunity) = await SeedAsync();
        var directory = StubPrincipalDirectory.Permitting(Rep);
        await using var context = _fixture.CreateAdminContext();

        await new ListAssignablePrincipalsHandler(context, StubAuthorizer.AlwaysAllow, directory).HandleAsync(Query(tenant, opportunity.Id, take: 0));

        Assert.Equal(ListAssignablePrincipalsHandler.DefaultTake, Assert.Single(directory.Listings).Take);
    }
}
