using Contracts;
using MasterData.Application;
using MasterData.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MasterData.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class MergePartyHandlerTests
{
    private readonly PostgresFixture _fixture;

    public MergePartyHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Merging_writes_state_outbox_and_evidence_together()
    {
        var tenant = TestData.NextTenant();
        long aId, bId;
        await using (var seed = _fixture.CreateAdminContext())
        {
            var a = Party.Create(tenant, PartyType.Organization, "A");
            var b = Party.Create(tenant, PartyType.Organization, "B (duplicate)");
            seed.Parties.AddRange(a, b);
            await seed.SaveChangesAsync();
            aId = a.Id;
            bId = b.Id;
        }

        var command = new MergePartyCommand(tenant, bId, aId, TestData.Operator, "merge-1", Guid.NewGuid());
        await using (var context = _fixture.CreateAdminContext())
        {
            var result = await new MergePartyHandler(context).HandleAsync(command);
            Assert.Equal(aId, result.CanonicalPartyId);
        }

        await using var verification = _fixture.CreateAdminContext();
        var mergedB = await verification.Parties.AsNoTracking().SingleAsync(p => p.Id == bId);
        var outbox = await verification.OutboxMessages.AsNoTracking().Where(m => m.AggregateId == bId).ToListAsync();
        var evidence = await verification.EvidenceRecords.AsNoTracking().Where(e => e.AggregateId == bId).ToListAsync();

        Assert.Equal(aId, mergedB.MergedIntoPartyId);
        Assert.Single(outbox);
        Assert.Single(evidence);
    }

    [Fact]
    public async Task Merging_into_an_already_merged_target_resolves_to_its_canonical()
    {
        var tenant = TestData.NextTenant();
        long aId, bId, cId;
        await using (var seed = _fixture.CreateAdminContext())
        {
            var a = Party.Create(tenant, PartyType.Organization, "A");
            var b = Party.Create(tenant, PartyType.Organization, "B");
            var c = Party.Create(tenant, PartyType.Organization, "C");
            seed.Parties.AddRange(a, b, c);
            await seed.SaveChangesAsync();
            aId = a.Id; bId = b.Id; cId = c.Id;
        }

        await using (var context = _fixture.CreateAdminContext())
            await new MergePartyHandler(context).HandleAsync(new MergePartyCommand(tenant, bId, aId, TestData.Operator, "merge-2a", Guid.NewGuid()));

        // C merges "into B" — B is itself already a tombstone; must resolve to A.
        MergePartyResult result;
        await using (var context = _fixture.CreateAdminContext())
            result = await new MergePartyHandler(context).HandleAsync(new MergePartyCommand(tenant, cId, bId, TestData.Operator, "merge-2b", Guid.NewGuid()));

        Assert.Equal(aId, result.CanonicalPartyId);

        await using var verification = _fixture.CreateAdminContext();
        var mergedC = await verification.Parties.AsNoTracking().SingleAsync(p => p.Id == cId);
        Assert.Equal(aId, mergedC.MergedIntoPartyId);
    }

    [Fact]
    public async Task External_identities_move_to_the_survivor_at_merge_time()
    {
        var tenant = TestData.NextTenant();
        long aId, bId;
        await using (var seed = _fixture.CreateAdminContext())
        {
            var a = Party.Create(tenant, PartyType.Organization, "A");
            var b = Party.Create(tenant, PartyType.Organization, "B (duplicate)");
            seed.Parties.AddRange(a, b);
            await seed.SaveChangesAsync();
            aId = a.Id; bId = b.Id;

            seed.PartyExternalIdentities.Add(PartyExternalIdentity.Create(tenant, bId, "sap", "sap-connection-a", null, "1001"));
            await seed.SaveChangesAsync();
        }

        await using (var context = _fixture.CreateAdminContext())
            await new MergePartyHandler(context).HandleAsync(new MergePartyCommand(tenant, bId, aId, TestData.Operator, "merge-3", Guid.NewGuid()));

        await using var verification = _fixture.CreateAdminContext();
        var identity = await verification.PartyExternalIdentities.AsNoTracking()
            .SingleAsync(e => e.TenantId == tenant && e.ExternalId == "1001");
        Assert.Equal(aId, identity.PartyId);
    }
}
