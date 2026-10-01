using Contracts;
using CRM.Application;
using CRM.Domain;
using CRM.Tests;
using CRM.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Application;

[Collection(nameof(PostgresCollection))]
public sealed class CreateOpportunityHandlerTests
{
    private readonly PostgresFixture _fixture;

    public CreateOpportunityHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Creating_persists_a_draft_opportunity_with_evidence_and_outbox()
    {
        var tenant = TestData.NextTenant();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");

        var command = new CreateOpportunityCommand(tenant, partyRef, TestData.Seller, "TRY", 1000m, "key-1", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        var result = await new CreateOpportunityHandler(context, StubAuthorizer.AlwaysAllow, Resolver(_fixture), StubDefinitionReader.None).HandleAsync(command);

        Assert.False(result.Replayed);
        Assert.True(result.OpportunityId > 0);

        var opportunity = await context.Opportunities.AsNoTracking().SingleAsync(o => o.Id == result.OpportunityId);
        Assert.Equal(OpportunityStatus.Draft, opportunity.Status);
        Assert.Single(await context.OutboxMessages.AsNoTracking().Where(m => m.AggregateId == result.OpportunityId).ToListAsync());
        Assert.Single(await context.EvidenceRecords.AsNoTracking().Where(e => e.AggregateId == result.OpportunityId).ToListAsync());
    }

    [Fact]
    public async Task Creating_is_denied_without_a_grant_for_the_action()
    {
        var tenant = TestData.NextTenant();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");
        var command = new CreateOpportunityCommand(tenant, partyRef, TestData.Seller, "TRY", 1000m, "key-denied", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<OpportunityAuthorizationDeniedException>(() =>
            new CreateOpportunityHandler(context, StubAuthorizer.AlwaysDeny, StubPartyIdentityResolver.Identity, StubDefinitionReader.None).HandleAsync(command));

        Assert.Empty(await context.Opportunities.AsNoTracking().Where(o => o.PartyRefPartyId == partyRef.PartyId).ToListAsync());
    }

    [Fact]
    public async Task Retrying_with_the_same_key_replays_the_same_opportunity_id()
    {
        var tenant = TestData.NextTenant();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");
        var command = new CreateOpportunityCommand(tenant, partyRef, TestData.Seller, "TRY", 1000m, "key-2", Guid.NewGuid());

        await using var first = _fixture.CreateAdminContext();
        var firstResult = await new CreateOpportunityHandler(first, StubAuthorizer.AlwaysAllow, Resolver(_fixture), StubDefinitionReader.None).HandleAsync(command);

        await using var second = _fixture.CreateAdminContext();
        var replay = await new CreateOpportunityHandler(second, StubAuthorizer.AlwaysAllow, Resolver(_fixture), StubDefinitionReader.None).HandleAsync(command);

        Assert.True(replay.Replayed);
        Assert.Equal(firstResult.OpportunityId, replay.OpportunityId);
    }

    private static MasterData.Application.PartyIdentityResolver Resolver(PostgresFixture fixture) =>
        new(fixture.CreateMasterDataContext());

    [Fact]
    public async Task Creating_for_a_party_that_does_not_exist_is_refused_and_writes_nothing()
    {
        var tenant = TestData.NextTenant();
        var command = new CreateOpportunityCommand(tenant, new PartyRef(tenant, 987_654_321), TestData.Seller, "TRY", 1000m, "key-unknown-party", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<PartyNotFoundException>(() =>
            new CreateOpportunityHandler(context, StubAuthorizer.AlwaysAllow, Resolver(_fixture), StubDefinitionReader.None).HandleAsync(command));

        Assert.Empty(await context.Opportunities.AsNoTracking().Where(o => o.TenantId == tenant).ToListAsync());
        Assert.Empty(await context.IdempotencyRecords.AsNoTracking().Where(r => r.TenantId == tenant).ToListAsync());
    }

    [Fact]
    public async Task A_party_of_another_tenant_is_refused_like_a_missing_one()
    {
        var owner = TestData.NextTenant();
        var other = TestData.NextTenant();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var foreign = await TestData.CreatePartyAsync(seedMasterData, other, "Elsewhere");
        var command = new CreateOpportunityCommand(owner, new PartyRef(owner, foreign.PartyId), TestData.Seller, "TRY", 1000m, "key-foreign-party", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<PartyNotFoundException>(() =>
            new CreateOpportunityHandler(context, StubAuthorizer.AlwaysAllow, Resolver(_fixture), StubDefinitionReader.None).HandleAsync(command));
    }

    [Fact]
    public async Task A_merged_party_is_stored_as_its_survivor()
    {
        var tenant = TestData.NextTenant();
        var merged = new PartyRef(tenant, 11);
        var survivor = new PartyRef(tenant, 22);
        var resolver = new StubPartyIdentityResolver(p => p == merged ? survivor : p);
        var command = new CreateOpportunityCommand(tenant, merged, TestData.Seller, "TRY", 1000m, "key-merged", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        var result = await new CreateOpportunityHandler(context, StubAuthorizer.AlwaysAllow, resolver, StubDefinitionReader.None).HandleAsync(command);

        var stored = await context.Opportunities.AsNoTracking().SingleAsync(o => o.Id == result.OpportunityId);
        Assert.Equal(survivor.PartyId, stored.PartyRefPartyId);
        var payload = await context.OutboxMessages.AsNoTracking().SingleAsync(m => m.AggregateId == result.OpportunityId);
        using var document = System.Text.Json.JsonDocument.Parse(payload.Payload);
        Assert.Equal(survivor.PartyId, document.RootElement.GetProperty("PartyId").GetInt64());
    }

    [Fact]
    public async Task A_replay_still_answers_after_the_party_is_gone_and_does_not_consult_the_resolver()
    {
        var tenant = TestData.NextTenant();
        var party = new PartyRef(tenant, 33);
        var command = new CreateOpportunityCommand(tenant, party, TestData.Seller, "TRY", 1000m, "key-replay-gone", Guid.NewGuid());

        await using var first = _fixture.CreateAdminContext();
        var created = await new CreateOpportunityHandler(first, StubAuthorizer.AlwaysAllow, StubPartyIdentityResolver.Identity, StubDefinitionReader.None).HandleAsync(command);

        var gone = new StubPartyIdentityResolver(_ => null);
        await using var second = _fixture.CreateAdminContext();
        var replay = await new CreateOpportunityHandler(second, StubAuthorizer.AlwaysAllow, gone, StubDefinitionReader.None).HandleAsync(command);

        Assert.True(replay.Replayed);
        Assert.Equal(created.OpportunityId, replay.OpportunityId);
        Assert.Equal(0, gone.Calls);
    }

    [Fact]
    public async Task A_retry_after_a_merge_is_a_replay_not_a_key_reuse_conflict()
    {
        var tenant = TestData.NextTenant();
        var merged = new PartyRef(tenant, 44);
        var command = new CreateOpportunityCommand(tenant, merged, TestData.Seller, "TRY", 1000m, "key-retry-after-merge", Guid.NewGuid());

        await using var first = _fixture.CreateAdminContext();
        var created = await new CreateOpportunityHandler(first, StubAuthorizer.AlwaysAllow, new StubPartyIdentityResolver(p => p == merged ? new PartyRef(tenant, 55) : p), StubDefinitionReader.None).HandleAsync(command);

        await using var second = _fixture.CreateAdminContext();
        var replay = await new CreateOpportunityHandler(second, StubAuthorizer.AlwaysAllow, StubPartyIdentityResolver.Identity, StubDefinitionReader.None).HandleAsync(command);

        Assert.True(replay.Replayed);
        Assert.Equal(created.OpportunityId, replay.OpportunityId);
    }
}
