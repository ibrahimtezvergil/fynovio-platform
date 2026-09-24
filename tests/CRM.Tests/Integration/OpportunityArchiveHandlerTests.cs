using Contracts;
using CRM.Application;
using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class OpportunityArchiveHandlerTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Archive_is_idempotent_and_writes_evidence_and_outbox_without_changing_lifecycle()
    {
        var (tenant, id) = await SeedDraftAsync();
        var command = Command(tenant, id, "archive-once", version: 1, archive: true);
        await using (var first = fixture.CreateAdminContext())
            Assert.False((await new SetOpportunityArchiveHandler(first, StubAuthorizer.AlwaysAllow).HandleAsync(command)).Replayed);
        await using (var replayContext = fixture.CreateAdminContext())
            Assert.True((await new SetOpportunityArchiveHandler(replayContext, StubAuthorizer.AlwaysAllow).HandleAsync(command)).Replayed);

        await using var verify = fixture.CreateAdminContext();
        var opportunity = await verify.Opportunities.AsNoTracking().SingleAsync(o => o.Id == id);
        Assert.Equal(OpportunityStatus.Draft, opportunity.Status);
        Assert.True(opportunity.IsArchived);
        Assert.NotNull(opportunity.ArchivedAt);
        Assert.Single(await verify.OutboxMessages.Where(item => item.AggregateId == id).ToListAsync());
        Assert.Single(await verify.EvidenceRecords.Where(item => item.AggregateId == id).ToListAsync());
    }

    [Fact]
    public async Task Archive_list_and_active_list_are_mutually_exclusive_and_stale_writes_conflict()
    {
        var (tenant, id) = await SeedDraftAsync();
        await using (var context = fixture.CreateAdminContext())
            await new SetOpportunityArchiveHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(Command(tenant, id, "archive-list", version: 1, archive: true));

        await using var queryContext = fixture.CreateAdminContext();
        var active = await new ListOpportunitiesHandler(queryContext, new StubScopeResolver(new AccessScope.All()))
            .HandleAsync(new ListOpportunitiesQuery(tenant, TestData.Seller, Guid.NewGuid(), null, 0, 50));
        var archived = await new ListOpportunitiesHandler(queryContext, new StubScopeResolver(new AccessScope.All()))
            .HandleAsync(new ListOpportunitiesQuery(tenant, TestData.Seller, Guid.NewGuid(), null, 0, 50, ArchivedOnly: true));
        Assert.DoesNotContain(active, item => item.Id == id);
        Assert.Contains(archived, item => item.Id == id && item.IsArchived);

        await using var staleContext = fixture.CreateAdminContext();
        await Assert.ThrowsAsync<OpportunityConcurrencyConflictException>(() => new SetOpportunityArchiveHandler(staleContext, StubAuthorizer.AlwaysAllow)
            .HandleAsync(Command(tenant, id, "archive-stale", version: 1, archive: false)));
    }

    [Fact]
    public async Task Archive_command_cannot_cross_tenant_boundary()
    {
        var (ownerTenant, id) = await SeedDraftAsync();
        var otherTenant = TestData.NextTenant();
        await using var context = fixture.CreateAdminContext();
        await Assert.ThrowsAsync<OpportunityNotFoundException>(() => new SetOpportunityArchiveHandler(context, StubAuthorizer.AlwaysAllow)
            .HandleAsync(Command(otherTenant, id, "cross-tenant-archive", version: 1, archive: true)));

        await using var verify = fixture.CreateAdminContext();
        Assert.False((await verify.Opportunities.AsNoTracking().SingleAsync(item => item.Id == id && item.TenantId == ownerTenant)).IsArchived);
    }

    private async Task<(TenantId TenantId, long Id)> SeedDraftAsync()
    {
        var tenant = TestData.NextTenant();
        await using var crm = fixture.CreateAdminContext();
        await using var masterData = fixture.CreateMasterDataContext();
        var party = await TestData.CreatePartyAsync(masterData, tenant, "Archive test");
        var opportunity = Opportunity.Create(tenant, party, TestData.Seller, "TRY", 250m);
        crm.Opportunities.Add(opportunity);
        await crm.SaveChangesAsync();
        return (tenant, opportunity.Id);
    }

    private static SetOpportunityArchiveCommand Command(TenantId tenant, long id, string key, long version, bool archive) =>
        new(tenant, id, TestData.Seller, version, archive, ConfirmOpenArchive: false, RestoreStageId: null, key, Guid.NewGuid());
}
