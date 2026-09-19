using Contracts;
using CRM.Application;
using CRM.Domain;
using CRM.Tests;
using CRM.Tests.Integration;
using Xunit;

namespace CRM.Tests.Application;

[Collection(nameof(PostgresCollection))]
public sealed class GetOpportunityAvailableActionsHandlerTests
{
    private readonly PostgresFixture _fixture;

    public GetOpportunityAvailableActionsHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task An_open_opportunity_with_a_billable_line_can_win_lose_change_stage_and_reassign()
    {
        var (tenant, opportunityId, entryStage, otherStage) = await SeedOpenOpportunityWithBillableLineAsync();

        await using var context = _fixture.CreateAdminContext();
        var actions = await new GetOpportunityAvailableActionsHandler(context, StubAuthorizer.AlwaysAllow)
            .HandleAsync(new GetOpportunityAvailableActionsQuery(tenant, opportunityId, TestData.Seller, Guid.NewGuid()));

        Assert.NotNull(actions);
        Assert.False(actions!.CanOpen); // already open
        Assert.True(actions.CanChangeStage);
        Assert.Contains(otherStage.Id, actions.AllowedTargetStageIds);
        Assert.DoesNotContain(entryStage.Id, actions.AllowedTargetStageIds); // current stage excluded
        Assert.True(actions.CanWin);
        Assert.True(actions.CanLose);
        Assert.True(actions.CanReassign);
    }

    [Fact]
    public async Task A_draft_opportunity_can_only_open_and_lose()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");
        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        await using var context = _fixture.CreateAdminContext();
        var actions = await new GetOpportunityAvailableActionsHandler(context, StubAuthorizer.AlwaysAllow)
            .HandleAsync(new GetOpportunityAvailableActionsQuery(tenant, opportunity.Id, TestData.Seller, Guid.NewGuid()));

        Assert.True(actions!.CanOpen);
        Assert.False(actions.CanChangeStage);
        Assert.False(actions.CanWin);
        Assert.True(actions.CanLose);
        Assert.True(actions.CanReassign);
    }

    [Fact]
    public async Task Denied_authorization_makes_that_action_unavailable_even_when_the_domain_guard_passes()
    {
        var (tenant, opportunityId, _, _) = await SeedOpenOpportunityWithBillableLineAsync();

        await using var context = _fixture.CreateAdminContext();
        var actions = await new GetOpportunityAvailableActionsHandler(context, new DenyingAuthorizer("crm.opportunity.win"))
            .HandleAsync(new GetOpportunityAvailableActionsQuery(tenant, opportunityId, TestData.Seller, Guid.NewGuid()));

        Assert.False(actions!.CanWin); // domain guard (Open + billable line) passes, but authorization is denied
        Assert.True(actions.CanLose); // a different action, still allowed, proves denial is scoped to the one action key
    }

    private async Task<(TenantId TenantId, long OpportunityId, PipelineStage EntryStage, PipelineStage OtherStage)> SeedOpenOpportunityWithBillableLineAsync()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");
        var definition = PipelineDefinition.Create(tenant, "Sales");
        seed.PipelineDefinitions.Add(definition);
        await seed.SaveChangesAsync();
        var version = definition.AddVersion(1);
        seed.PipelineDefinitionVersions.Add(version);
        await seed.SaveChangesAsync();
        var entryStage = version.AddStage("Bekliyor", 0);
        var otherStage = version.AddStage("Teklif Verildi", 1);
        seed.PipelineStages.AddRange(entryStage, otherStage);
        await seed.SaveChangesAsync();

        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        opportunity.AddLine(TestData.ProductRef(tenant), 1, 1000m);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), version.Id, entryStage.Id);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        return (tenant, opportunity.Id, entryStage, otherStage);
    }

    private sealed class DenyingAuthorizer(string deniedActionKey) : IAuthorizer
    {
        public Task<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AuthorizationDecision(
                request.Action.Value == deniedActionKey ? AuthorizationEffect.Deny : AuthorizationEffect.Allow,
                request.Action.Value == deniedActionKey ? "stub_deny" : "stub_allow",
                Guid.NewGuid(), 0));
    }
}
