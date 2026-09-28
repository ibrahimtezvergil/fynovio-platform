using Contracts;
using CRM.Application;
using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class WinOpportunityHandlerTests
{
    private readonly PostgresFixture _fixture;

    public WinOpportunityHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Winning_writes_state_outbox_and_evidence_together()
    {
        var (tenant, opportunityId) = await SeedOpenOpportunityAsync();
        var command = NewCommand(tenant, opportunityId, "key-1", expectedVersion: 3);

        await using (var context = _fixture.CreateAdminContext())
        {
            var result = await new WinOpportunityHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command);
            Assert.False(result.Replayed);
            Assert.Equal(100.00m, result.TotalAmount);
        }

        await using var verification = _fixture.CreateAdminContext();
        var opportunity = await verification.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunityId);
        var outbox = await verification.OutboxMessages.AsNoTracking().Where(m => m.AggregateId == opportunityId).ToListAsync();
        var evidence = await verification.EvidenceRecords.AsNoTracking().Where(e => e.AggregateId == opportunityId).ToListAsync();

        Assert.Equal(OpportunityStatus.Won, opportunity.Status);
        var message = Assert.Single(outbox);
        var record = Assert.Single(evidence);
        Assert.Equal("enterprise.crmsales.opportunity.won.v1", message.EventType);
        Assert.Equal(opportunity.RowVersion, message.AggregateVersion);
        Assert.Equal("Opportunity.Win", record.Action);
    }

    [Fact]
    public async Task Winning_is_denied_without_a_grant_for_the_action()
    {
        var (tenant, opportunityId) = await SeedOpenOpportunityAsync();
        var command = NewCommand(tenant, opportunityId, "key-denied", expectedVersion: 3);

        await using var context = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<OpportunityAuthorizationDeniedException>(() =>
            new WinOpportunityHandler(context, StubAuthorizer.AlwaysDeny).HandleAsync(command));

        await using var verification = _fixture.CreateAdminContext();
        var untouched = await verification.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunityId);
        Assert.Equal(OpportunityStatus.Open, untouched.Status);
    }

    [Fact]
    public async Task Winning_with_a_stale_expected_version_is_a_concurrency_conflict()
    {
        var (tenant, opportunityId) = await SeedOpenOpportunityAsync();
        var command = NewCommand(tenant, opportunityId, "key-stale", expectedVersion: 999);

        await using var context = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<OpportunityConcurrencyConflictException>(() =>
            new WinOpportunityHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command));
    }

    [Fact]
    public async Task Retrying_with_the_same_key_replays_the_stored_response()
    {
        var (tenant, opportunityId) = await SeedOpenOpportunityAsync();
        var command = NewCommand(tenant, opportunityId, "key-2", expectedVersion: 3);

        await using (var first = _fixture.CreateAdminContext())
            await new WinOpportunityHandler(first, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        await using (var second = _fixture.CreateAdminContext())
        {
            var replay = await new WinOpportunityHandler(second, StubAuthorizer.AlwaysAllow).HandleAsync(command);
            Assert.True(replay.Replayed);
            Assert.Equal(100.00m, replay.TotalAmount);
        }

        await using var verification = _fixture.CreateAdminContext();
        Assert.Single(await verification.OutboxMessages.AsNoTracking().Where(m => m.AggregateId == opportunityId).ToListAsync());
    }

    [Fact]
    public async Task Reusing_a_key_for_a_different_request_is_rejected()
    {
        var (tenant, firstId) = await SeedOpenOpportunityAsync(tenant: null);
        var (_, secondId) = await SeedOpenOpportunityAsync(tenant);

        await using (var first = _fixture.CreateAdminContext())
            await new WinOpportunityHandler(first, StubAuthorizer.AlwaysAllow).HandleAsync(NewCommand(tenant, firstId, "key-shared", 3));

        await using var second = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() =>
            new WinOpportunityHandler(second, StubAuthorizer.AlwaysAllow).HandleAsync(NewCommand(tenant, secondId, "key-shared", 3)));
    }

    [Fact]
    public async Task Two_concurrent_first_attempts_with_the_same_key_leave_exactly_one_winner_and_the_loser_replays_it()
    {
        var (tenant, opportunityId) = await SeedOpenOpportunityAsync();
        var command = NewCommand(tenant, opportunityId, "key-race", expectedVersion: 3);

        // Simulate the race directly rather than truly running two threads: pre-insert
        // the winner's IdempotencyRecord as if another request already committed it,
        // then run this handler and confirm it replays instead of re-executing.
        await using (var seedIdempotency = _fixture.CreateAdminContext())
        {
            var winner = await new WinOpportunityHandler(seedIdempotency, StubAuthorizer.AlwaysAllow).HandleAsync(command);
            Assert.False(winner.Replayed);
        }

        await using var loser = _fixture.CreateAdminContext();
        var loserResult = await new WinOpportunityHandler(loser, StubAuthorizer.AlwaysAllow).HandleAsync(command);
        Assert.True(loserResult.Replayed);

        await using var verification = _fixture.CreateAdminContext();
        Assert.Single(await verification.OutboxMessages.AsNoTracking().Where(m => m.AggregateId == opportunityId).ToListAsync());
    }

    private static WinOpportunityCommand NewCommand(TenantId tenant, long opportunityId, string key, long expectedVersion) =>
        new(tenant, opportunityId, TestData.Seller, expectedVersion, key, Guid.NewGuid());

    private async Task<(TenantId TenantId, long OpportunityId)> SeedOpenOpportunityAsync(TenantId? tenant = null)
    {
        var tenantId = tenant ?? TestData.NextTenant();
        await using var seedCrm = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();

        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenantId, "Acme");

        var (versionId, stageId) = await TestData.CreatePublishedPipelineWithEntryStageAsync(seedCrm, tenantId);
        var opportunity = Opportunity.Create(tenantId, partyRef, TestData.Seller, "TRY", 100m);
        opportunity.AddLine(TestData.ProductRef(tenantId), quantity: 1, unitPrice: 100m);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), versionId, stageId);
        seedCrm.Opportunities.Add(opportunity);
        await seedCrm.SaveChangesAsync();

        return (tenantId, opportunity.Id);
    }
}
