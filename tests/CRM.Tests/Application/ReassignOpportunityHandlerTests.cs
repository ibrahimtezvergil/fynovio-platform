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
        var result = await new ReassignOpportunityHandler(context, StubAuthorizer.AlwaysAllow, StubPrincipalDirectory.Permitting(newOwner)).HandleAsync(command);

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
            new ReassignOpportunityHandler(context, StubAuthorizer.AlwaysDeny, StubPrincipalDirectory.Permitting(newOwner)).HandleAsync(command));

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
            var firstResult = await new ReassignOpportunityHandler(first, StubAuthorizer.AlwaysAllow, StubPrincipalDirectory.Permitting(newOwner)).HandleAsync(command);
            Assert.False(firstResult.Replayed);
        }

        await using var second = _fixture.CreateAdminContext();
        var replay = await new ReassignOpportunityHandler(second, StubAuthorizer.AlwaysAllow, StubPrincipalDirectory.Permitting(newOwner)).HandleAsync(command);

        Assert.True(replay.Replayed);
        Assert.Equal(opportunity.Id, replay.OpportunityId);

        await using var verification = _fixture.CreateAdminContext();
        Assert.Single(await verification.OutboxMessages.AsNoTracking().Where(m => m.AggregateId == opportunity.Id).ToListAsync());
        Assert.Single(await verification.EvidenceRecords.AsNoTracking().Where(e => e.AggregateId == opportunity.Id).ToListAsync());
    }

    private async Task<(TenantId Tenant, Opportunity Opportunity)> SeedOpportunityAsync()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");
        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();
        return (tenant, opportunity);
    }

    [Fact]
    public async Task A_target_the_directory_does_not_permit_is_refused_and_nothing_is_written()
    {
        var (tenant, opportunity) = await SeedOpportunityAsync();
        var stranger = new PrincipalRef("https://idp.local", "not-a-member");
        var directory = StubPrincipalDirectory.Nobody;
        var command = new ReassignOpportunityCommand(tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion, stranger, "key-stranger", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<PrincipalNotAssignableException>(() =>
            new ReassignOpportunityHandler(context, StubAuthorizer.AlwaysAllow, directory).HandleAsync(command));

        await using var verify = _fixture.CreateAdminContext();
        Assert.Equal(TestData.Seller, (await verify.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunity.Id)).AssignedPrincipal);
        Assert.Empty(await verify.OutboxMessages.AsNoTracking().Where(m => m.AggregateId == opportunity.Id).ToListAsync());
        Assert.Empty(await verify.EvidenceRecords.AsNoTracking().Where(e => e.AggregateId == opportunity.Id).ToListAsync());
        Assert.Empty(await verify.IdempotencyRecords.AsNoTracking().Where(r => r.TenantId == tenant && r.IdempotencyKey == "key-stranger").ToListAsync());
    }

    [Fact]
    public async Task The_target_is_checked_against_the_crm_assignee_requirement()
    {
        var (tenant, opportunity) = await SeedOpportunityAsync();
        var newOwner = new PrincipalRef("https://idp.local", "seller-2");
        var directory = StubPrincipalDirectory.Permitting(newOwner);
        var command = new ReassignOpportunityCommand(tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion, newOwner, "key-policy", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        await new ReassignOpportunityHandler(context, StubAuthorizer.AlwaysAllow, directory).HandleAsync(command);

        var asked = Assert.Single(directory.AskedActions);
        Assert.Equal(["crm.opportunity.read", "crm.opportunity.change_stage"], asked.Select(a => a.Value));
    }

    [Fact]
    public async Task Authorization_runs_before_the_target_is_examined()
    {
        var (tenant, opportunity) = await SeedOpportunityAsync();
        var directory = StubPrincipalDirectory.Nobody;
        var command = new ReassignOpportunityCommand(tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion,
            new PrincipalRef("https://idp.local", "anyone"), "key-order", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<OpportunityAuthorizationDeniedException>(() =>
            new ReassignOpportunityHandler(context, StubAuthorizer.AlwaysDeny, directory).HandleAsync(command));

        Assert.Equal(0, directory.Calls); // a denied actor learns nothing about who is a member
    }

    [Fact]
    public async Task A_replay_of_a_committed_reassignment_does_not_re_examine_the_target()
    {
        var (tenant, opportunity) = await SeedOpportunityAsync();
        var newOwner = new PrincipalRef("https://idp.local", "seller-2");
        var command = new ReassignOpportunityCommand(tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion, newOwner, "key-late", Guid.NewGuid());
        await using (var first = _fixture.CreateAdminContext())
            await new ReassignOpportunityHandler(first, StubAuthorizer.AlwaysAllow, StubPrincipalDirectory.Permitting(newOwner)).HandleAsync(command);

        var nobodyNow = StubPrincipalDirectory.Nobody;
        await using var second = _fixture.CreateAdminContext();
        var replay = await new ReassignOpportunityHandler(second, StubAuthorizer.AlwaysAllow, nobodyNow).HandleAsync(command);

        Assert.True(replay.Replayed);
        Assert.Equal(0, nobodyNow.Calls);
    }

    [Fact]
    public async Task A_stale_version_is_still_a_concurrency_conflict_for_a_permitted_target()
    {
        var (tenant, opportunity) = await SeedOpportunityAsync();
        var newOwner = new PrincipalRef("https://idp.local", "seller-2");
        var command = new ReassignOpportunityCommand(tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion + 5, newOwner, "key-stale", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<OpportunityConcurrencyConflictException>(() =>
            new ReassignOpportunityHandler(context, StubAuthorizer.AlwaysAllow, StubPrincipalDirectory.Permitting(newOwner)).HandleAsync(command));
    }
}
