using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Integration;

/// <summary>CRM_Phase1_Test_Coverage_Verification_Report.pdf T-13/T-14: proves the migration
/// chain correctly upgrades pre-existing (N-1) rows, not just that a fresh database reaches the
/// latest schema. See LegacyMigrationFixture for how the legacy rows are seeded.</summary>
public sealed class LegacyDataMigrationTests : IClassFixture<LegacyMigrationFixture>
{
    private readonly LegacyMigrationFixture _fixture;

    public LegacyDataMigrationTests(LegacyMigrationFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("legacy-waiting", OpportunityStatus.Draft)]
    [InlineData("legacy-offered", OpportunityStatus.Open)]
    [InlineData("legacy-completed", OpportunityStatus.Won)]
    [InlineData("legacy-canceled", OpportunityStatus.Lost)]
    public async Task Legacy_status_remaps_to_the_new_vocabulary(string subject, OpportunityStatus expected)
    {
        await using var context = _fixture.CreateCrmContext();

        var opportunity = await context.Opportunities.AsNoTracking()
            .SingleAsync(o => o.TenantId == _fixture.Tenant && o.AssignedPrincipalSubject == subject);

        Assert.Equal(expected, opportunity.Status);
    }

    [Fact]
    public async Task Legacy_completed_opportunitys_sale_date_survives_as_won_date()
    {
        await using var context = _fixture.CreateCrmContext();

        var opportunity = await context.Opportunities.AsNoTracking()
            .SingleAsync(o => o.TenantId == _fixture.Tenant && o.AssignedPrincipalSubject == "legacy-completed");

        Assert.NotNull(opportunity.WonDate);
    }

    [Fact]
    public async Task Legacy_canceled_opportunitys_reason_and_date_survive_as_lost_fields()
    {
        await using var context = _fixture.CreateCrmContext();

        var opportunity = await context.Opportunities.AsNoTracking()
            .SingleAsync(o => o.TenantId == _fixture.Tenant && o.AssignedPrincipalSubject == "legacy-canceled");

        Assert.Equal("bütçe iptal oldu", opportunity.LostReason);
        Assert.NotNull(opportunity.LostDate);
    }

    [Fact]
    public async Task Legacy_opportunitys_party_id_survives_the_rename_to_party_ref_party_id()
    {
        await using var crmContext = _fixture.CreateCrmContext();
        await using var masterDataContext = _fixture.CreateMasterDataContext();

        var opportunity = await crmContext.Opportunities.AsNoTracking()
            .SingleAsync(o => o.TenantId == _fixture.Tenant && o.AssignedPrincipalSubject == "legacy-waiting");
        var party = await masterDataContext.Parties.AsNoTracking()
            .SingleAsync(p => p.Id == opportunity.PartyRefPartyId);

        Assert.Equal("Legacy Acme", party.Name);
    }

    [Fact]
    public async Task Legacy_crm_party_backfills_into_masterdata_with_identity_and_tenant_preserved()
    {
        await using var crmContext = _fixture.CreateCrmContext();
        await using var masterDataContext = _fixture.CreateMasterDataContext();

        // crm.parties is gone by this point (DropCrmParties); recover the id through an
        // opportunity that referenced it before the drop.
        var opportunity = await crmContext.Opportunities.AsNoTracking()
            .SingleAsync(o => o.TenantId == _fixture.Tenant && o.AssignedPrincipalSubject == "legacy-waiting");

        var party = await masterDataContext.Parties.AsNoTracking()
            .SingleAsync(p => p.Id == opportunity.PartyRefPartyId);

        Assert.Equal(_fixture.Tenant, party.TenantId);
        Assert.Equal("Legacy Acme", party.Name);
        Assert.Equal("Corp", party.Surname);
        Assert.Equal("+90 555 000 00 00", party.Phone);
        Assert.Equal("legacy@acme.test", party.Email);
        Assert.Equal(Contracts.PartyType.Organization, party.PartyType);
    }
}
