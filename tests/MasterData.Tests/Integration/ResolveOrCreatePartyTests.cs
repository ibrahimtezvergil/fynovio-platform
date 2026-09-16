using Contracts;
using MasterData.Application;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MasterData.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class ResolveOrCreatePartyTests
{
    private readonly PostgresFixture _fixture;

    public ResolveOrCreatePartyTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task First_call_creates_second_call_resolves()
    {
        var tenant = TestData.NextTenant();
        var command = new ResolveOrCreatePartyCommand(
            tenant, "sap", "sap-connection-a", null, "1001", PartyType.Organization, "ABC AŞ", Guid.NewGuid());

        long partyId;
        await using (var first = _fixture.CreateAdminContext())
        {
            var result = await new ResolveOrCreatePartyHandler(first).HandleAsync(command);
            Assert.True(result.Created);
            partyId = result.PartyId;
        }

        await using (var second = _fixture.CreateAdminContext())
        {
            var result = await new ResolveOrCreatePartyHandler(second).HandleAsync(command);
            Assert.False(result.Created);
            Assert.Equal(partyId, result.PartyId);
        }

        await using var verification = _fixture.CreateAdminContext();
        var count = await verification.Parties.CountAsync(p => p.TenantId == tenant);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Two_connections_of_the_same_provider_do_not_collide()
    {
        var tenant = TestData.NextTenant();
        var connectionA = new ResolveOrCreatePartyCommand(tenant, "sap", "sap-connection-a", null, "1001", PartyType.Organization, "ABC AŞ", Guid.NewGuid());
        var connectionB = new ResolveOrCreatePartyCommand(tenant, "sap", "sap-connection-b", null, "1001", PartyType.Organization, "DEF AŞ", Guid.NewGuid());

        long partyIdA, partyIdB;
        await using (var context = _fixture.CreateAdminContext())
            partyIdA = (await new ResolveOrCreatePartyHandler(context).HandleAsync(connectionA)).PartyId;
        await using (var context = _fixture.CreateAdminContext())
            partyIdB = (await new ResolveOrCreatePartyHandler(context).HandleAsync(connectionB)).PartyId;

        Assert.NotEqual(partyIdA, partyIdB);
    }
}
