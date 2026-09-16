using Contracts;
using MasterData.Application;
using MasterData.Domain;
using Xunit;

namespace MasterData.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class PartyDirectoryTests
{
    private readonly PostgresFixture _fixture;

    public PartyDirectoryTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetParty_resolves_a_merged_party_to_its_survivor()
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
        }
        await using (var context = _fixture.CreateAdminContext())
            await new MergePartyHandler(context).HandleAsync(new MergePartyCommand(tenant, bId, aId, TestData.Operator, "merge-4", Guid.NewGuid()));

        await using var directoryContext = _fixture.CreateAdminContext();
        var directory = new PartyDirectory(directoryContext);

        var entry = await directory.GetPartyAsync(new PartyRef(tenant, bId));

        Assert.NotNull(entry);
        Assert.Equal("A", entry!.Name);
    }

    [Fact]
    public async Task GetParties_batch_lookup_resolves_all_requested_refs_in_one_call()
    {
        var tenant = TestData.NextTenant();
        long aId, bId;
        await using (var seed = _fixture.CreateAdminContext())
        {
            var a = Party.Create(tenant, PartyType.Organization, "A");
            var b = Party.Create(tenant, PartyType.Organization, "B");
            seed.Parties.AddRange(a, b);
            await seed.SaveChangesAsync();
            aId = a.Id; bId = b.Id;
        }

        await using var directoryContext = _fixture.CreateAdminContext();
        var directory = new PartyDirectory(directoryContext);

        var result = await directory.GetPartiesAsync([new PartyRef(tenant, aId), new PartyRef(tenant, bId)]);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task ResolveExternalIdentityAsync_follows_the_merge_chain()
    {
        var tenant = TestData.NextTenant();
        long aId, bId;
        await using (var seed = _fixture.CreateAdminContext())
        {
            var a = Party.Create(tenant, PartyType.Organization, "A");
            var b = Party.Create(tenant, PartyType.Organization, "B");
            seed.Parties.AddRange(a, b);
            await seed.SaveChangesAsync();
            aId = a.Id; bId = b.Id;
            seed.PartyExternalIdentities.Add(PartyExternalIdentity.Create(tenant, bId, "sap", "sap-connection-a", null, "1001"));
            await seed.SaveChangesAsync();
        }
        await using (var context = _fixture.CreateAdminContext())
            await new MergePartyHandler(context).HandleAsync(new MergePartyCommand(tenant, bId, aId, TestData.Operator, "merge-5", Guid.NewGuid()));

        await using var resolverContext = _fixture.CreateAdminContext();
        var resolver = new PartyIdentityResolver(resolverContext);

        var resolved = await resolver.ResolveExternalIdentityAsync(tenant, "sap", "sap-connection-a", null, "1001");

        Assert.Equal(aId, resolved!.Value.PartyId);
    }
}
