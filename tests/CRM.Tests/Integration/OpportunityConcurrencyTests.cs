using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Integration;

/// <summary>"En az bir aktif zorunlu satır" kuralı satırlar arası bir invariant'tır ve
/// tek satırlık CHECK ile ifade edilemez (docs/schema/crm-sales-schema.md). Bu yüzden
/// satır değişikliği aggregate root'un versiyonunu artırmalı, aksi halde iki eşzamanlı
/// işlem kuralı birlikte delebilir.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class OpportunityConcurrencyTests
{
    private readonly PostgresFixture _fixture;

    public OpportunityConcurrencyTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Canceling_the_last_required_line_conflicts_with_completing()
    {
        long opportunityId;
        var tenant = TestData.NextTenant();

        await using (var seedCrm = _fixture.CreateAdminContext())
        await using (var seedMasterData = _fixture.CreateMasterDataContext())
        {
            var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");

            var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
            opportunity.AddLine(TestData.ProductRef(tenant), quantity: 1, unitPrice: 100m);
            opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), null, null);
            seedCrm.Opportunities.Add(opportunity);
            await seedCrm.SaveChangesAsync();
            opportunityId = opportunity.Id;
        }

        await using var contextA = _fixture.CreateAdminContext();
        await using var contextB = _fixture.CreateAdminContext();

        var fromA = await contextA.Opportunities.Include(o => o.Lines)
            .SingleAsync(o => o.Id == opportunityId);
        var fromB = await contextB.Opportunities.Include(o => o.Lines)
            .SingleAsync(o => o.Id == opportunityId);

        fromB.CancelLine(fromB.Lines.Single(), "stokta yok");
        await contextB.SaveChangesAsync();

        fromA.Win();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextA.SaveChangesAsync());
    }
}
