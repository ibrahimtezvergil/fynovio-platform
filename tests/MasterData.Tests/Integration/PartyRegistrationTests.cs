using Contracts;
using MasterData.Application;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MasterData.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class PartyRegistrationTests
{
    private readonly PostgresFixture _fixture;

    public PartyRegistrationTests(PostgresFixture fixture) => _fixture = fixture;

    private static RegisterPartyRequest Request(TenantId tenant, string name, string key) =>
        new(tenant, PartyType.Organization, name, null, "+90 555", "hello@acme.test", key, Guid.NewGuid());

    [Fact]
    public async Task Registers_a_party_in_the_requested_tenant_and_reports_its_ref()
    {
        var tenant = TestData.NextTenant();
        await using var context = _fixture.CreateAdminContext();

        var result = await new PartyRegistration(new CreatePartyHandler(context)).RegisterAsync(Request(tenant, "Acme", "key-1"));

        Assert.False(result.Replayed);
        Assert.Equal(tenant, result.PartyRef.TenantId);
        await using var read = _fixture.CreateAdminContext();
        var party = await read.Parties.AsNoTracking().SingleAsync(p => p.Id == result.PartyRef.PartyId);
        Assert.Equal((tenant, "Acme", "+90 555", "hello@acme.test"), (party.TenantId, party.Name, party.Phone, party.Email));
    }

    [Fact]
    public async Task A_retry_with_the_same_key_replays_the_same_party()
    {
        var tenant = TestData.NextTenant();
        await using var first = _fixture.CreateAdminContext();
        var created = await new PartyRegistration(new CreatePartyHandler(first)).RegisterAsync(Request(tenant, "Acme", "key-replay"));

        await using var second = _fixture.CreateAdminContext();
        var replay = await new PartyRegistration(new CreatePartyHandler(second)).RegisterAsync(Request(tenant, "Acme", "key-replay"));

        Assert.True(replay.Replayed);
        Assert.Equal(created.PartyRef, replay.PartyRef);
    }

    [Fact]
    public async Task The_same_key_in_another_tenant_is_a_different_registration()
    {
        var first = TestData.NextTenant();
        var second = TestData.NextTenant();
        await using var one = _fixture.CreateAdminContext();
        await using var two = _fixture.CreateAdminContext();

        var a = await new PartyRegistration(new CreatePartyHandler(one)).RegisterAsync(Request(first, "Acme", "shared-key"));
        var b = await new PartyRegistration(new CreatePartyHandler(two)).RegisterAsync(Request(second, "Acme", "shared-key"));

        Assert.False(b.Replayed);
        Assert.NotEqual(a.PartyRef.PartyId, b.PartyRef.PartyId);
    }

    [Fact]
    public async Task The_same_key_with_another_request_is_refused()
    {
        var tenant = TestData.NextTenant();
        await using var first = _fixture.CreateAdminContext();
        await new PartyRegistration(new CreatePartyHandler(first)).RegisterAsync(Request(tenant, "Acme", "key-reused"));

        await using var second = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() =>
            new PartyRegistration(new CreatePartyHandler(second)).RegisterAsync(Request(tenant, "Other", "key-reused")));
    }
}
