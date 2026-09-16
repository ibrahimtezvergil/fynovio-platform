using Contracts;
using MasterData.Application;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MasterData.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class CreatePartyHandlerTests
{
    private readonly PostgresFixture _fixture;

    public CreatePartyHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Creating_writes_state_and_outbox_together()
    {
        var tenant = TestData.NextTenant();
        var command = new CreatePartyCommand(tenant, PartyType.Organization, "ABC AŞ", null, null, null, "key-1", Guid.NewGuid());

        long partyId;
        await using (var context = _fixture.CreateAdminContext())
        {
            var result = await new CreatePartyHandler(context).HandleAsync(command);
            Assert.False(result.Replayed);
            partyId = result.PartyId;
        }

        await using var verification = _fixture.CreateAdminContext();
        var party = await verification.Parties.AsNoTracking().SingleAsync(p => p.Id == partyId);
        var outbox = await verification.OutboxMessages.AsNoTracking().Where(m => m.AggregateId == partyId).ToListAsync();

        Assert.Equal("ABC AŞ", party.Name);
        Assert.Single(outbox);
        Assert.Equal("enterprise.masterdata.party.created.v1", outbox[0].EventType);
    }

    [Fact]
    public async Task Retrying_with_the_same_key_replays_the_stored_response()
    {
        var tenant = TestData.NextTenant();
        var command = new CreatePartyCommand(tenant, PartyType.Organization, "XYZ AŞ", null, null, null, "key-2", Guid.NewGuid());

        await using (var first = _fixture.CreateAdminContext())
            await new CreatePartyHandler(first).HandleAsync(command);

        await using (var second = _fixture.CreateAdminContext())
        {
            var result = await new CreatePartyHandler(second).HandleAsync(command);
            Assert.True(result.Replayed);
        }

        await using var verification = _fixture.CreateAdminContext();
        var count = await verification.Parties.CountAsync(p => p.TenantId == tenant);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Reusing_a_key_for_a_different_request_is_rejected()
    {
        var tenant = TestData.NextTenant();
        var command = new CreatePartyCommand(tenant, PartyType.Organization, "A", null, null, null, "key-3", Guid.NewGuid());
        var differentCommand = command with { Name = "B" };

        await using (var first = _fixture.CreateAdminContext())
            await new CreatePartyHandler(first).HandleAsync(command);

        await using var second = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() =>
            new CreatePartyHandler(second).HandleAsync(differentCommand));
    }
}
