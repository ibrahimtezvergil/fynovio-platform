using CRM.Domain;
using Contracts;
using MasterData.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Integration;

/// <summary>Verifies that the backfill migration correctly copied all data from crm.parties
/// to masterdata.parties. This is step 3 of docs/plans/2026-09-16-masterdata-party-foundation.md §9.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class PartyBackfillVerificationTests
{
    private readonly PostgresFixture _fixture;

    public PartyBackfillVerificationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Backfilled_masterdata_parties_matches_crm_parties_row_count()
    {
        await using var crmContext = _fixture.CreateAdminContext();
        await using var masterDataContext = _fixture.CreateMasterDataContext();

        var crmPartyCount = await crmContext.Parties.CountAsync();
        var masterDataPartyCount = await masterDataContext.Parties.CountAsync();

        Assert.Equal(crmPartyCount, masterDataPartyCount);
    }

    [Fact]
    public async Task Every_crm_party_exists_in_masterdata_with_correct_data()
    {
        await using var crmContext = _fixture.CreateAdminContext();
        await using var masterDataContext = _fixture.CreateMasterDataContext();

        var crmParties = await crmContext.Parties.OrderBy(p => p.Id).ToListAsync();
        var masterDataParties = await masterDataContext.Parties.OrderBy(p => p.Id).ToListAsync();

        Assert.Equal(crmParties.Count, masterDataParties.Count);

        for (int i = 0; i < crmParties.Count; i++)
        {
            var crmParty = crmParties[i];
            var masterDataParty = masterDataParties[i];

            Assert.Equal(crmParty.Id, masterDataParty.Id);
            Assert.Equal(crmParty.TenantId, masterDataParty.TenantId);
            Assert.Equal(crmParty.Name, masterDataParty.Name);
            Assert.Equal(crmParty.Surname, masterDataParty.Surname);
            Assert.Equal(crmParty.Phone, masterDataParty.Phone);
            Assert.Equal(crmParty.Email, masterDataParty.Email);
            Assert.Equal(crmParty.MergedIntoPartyId, masterDataParty.MergedIntoPartyId);
        }
    }

    [Fact]
    public async Task All_backfilled_parties_have_organization_type()
    {
        await using var masterDataContext = _fixture.CreateMasterDataContext();

        var nonOrganizationParties = await masterDataContext.Parties
            .Where(p => p.PartyType != Contracts.PartyType.Organization)
            .CountAsync();

        Assert.Equal(0, nonOrganizationParties);
    }
}
