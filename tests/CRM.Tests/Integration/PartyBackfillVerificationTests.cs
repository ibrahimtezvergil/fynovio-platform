using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Integration;

/// <summary>docs/plans/2026-09-16-masterdata-party-foundation.md §9 step 3 originally asked
/// for a crm.parties-vs-masterdata.parties row/column comparison, but that is no longer
/// testable here: crm.parties is dropped by the DropCrmParties migration, which runs (along
/// with the rest of history) on every fresh Testcontainers database — there is no "before the
/// drop" state to inspect in this harness. In a Testcontainers-fresh database crm.parties also
/// never holds real rows to begin with (no seed data predates the backfill migration), so a
/// row-count comparison would only ever have proven 0 == 0. What IS still worth covering here:
/// that PostgresFixture's dual-DbContext migration wiring and masterdata schema grants
/// (added in this same task) actually work — a real party seeded through
/// TestData.CreatePartyAsync is readable back through MasterDataDbContext.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class PartyBackfillVerificationTests
{
    private readonly PostgresFixture _fixture;

    public PartyBackfillVerificationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task A_party_seeded_through_masterdata_is_readable_back()
    {
        await using var masterDataContext = _fixture.CreateMasterDataContext();
        var tenant = TestData.NextTenant();

        var partyRef = await TestData.CreatePartyAsync(masterDataContext, tenant, "Acme");

        await using var verification = _fixture.CreateMasterDataContext();
        var party = await verification.Parties.AsNoTracking().SingleAsync(p => p.Id == partyRef.PartyId);

        Assert.Equal(tenant, party.TenantId);
        Assert.Equal("Acme", party.Name);
        Assert.Equal(Contracts.PartyType.Organization, party.PartyType);
    }
}
