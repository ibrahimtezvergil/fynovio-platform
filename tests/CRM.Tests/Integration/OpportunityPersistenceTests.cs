using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class OpportunityPersistenceTests
{
    private readonly PostgresFixture _fixture;

    public OpportunityPersistenceTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Canceling_a_waiting_opportunity_persists()
    {
        await using var context = _fixture.CreateAdminContext();
        var tenant = TestData.NextTenant();

        var party = Party.Create(tenant, "Acme", PartyCreationSource.Manual);
        context.Parties.Add(party);
        await context.SaveChangesAsync();

        var opportunity = Opportunity.Create(tenant, party.Id, TestData.Seller, "TRY", 1000m);
        context.Opportunities.Add(opportunity);
        await context.SaveChangesAsync();

        opportunity.Cancel("müşteri vazgeçti");
        await context.SaveChangesAsync();

        var reloaded = await context.Opportunities.AsNoTracking()
            .SingleAsync(o => o.Id == opportunity.Id);
        Assert.Equal(OpportunityStatus.Canceled, reloaded.Status);
    }

    [Fact]
    public async Task Canceling_an_offered_opportunity_persists()
    {
        await using var context = _fixture.CreateAdminContext();
        var tenant = TestData.NextTenant();

        var party = Party.Create(tenant, "Acme", PartyCreationSource.Manual);
        context.Parties.Add(party);
        await context.SaveChangesAsync();

        var opportunity = Opportunity.Create(tenant, party.Id, TestData.Seller, "TRY", 1000m);
        opportunity.Offer(DateTimeOffset.UtcNow.AddDays(7));
        context.Opportunities.Add(opportunity);
        await context.SaveChangesAsync();

        opportunity.Cancel("bütçe onaylanmadı");
        await context.SaveChangesAsync();

        var reloaded = await context.Opportunities.AsNoTracking()
            .SingleAsync(o => o.Id == opportunity.Id);
        Assert.Equal(OpportunityStatus.Canceled, reloaded.Status);
    }

    [Fact]
    public async Task Line_total_and_completed_total_are_persisted()
    {
        await using var context = _fixture.CreateAdminContext();
        var tenant = TestData.NextTenant();

        var party = Party.Create(tenant, "Acme", PartyCreationSource.Manual);
        context.Parties.Add(party);
        await context.SaveChangesAsync();

        var opportunity = Opportunity.Create(tenant, party.Id, TestData.Seller, "TRY", 0m);
        opportunity.AddLine(TestData.ProductRef(tenant), quantity: 3, unitPrice: 33.33m);
        opportunity.Offer(DateTimeOffset.UtcNow.AddDays(7));
        opportunity.Complete();
        context.Opportunities.Add(opportunity);
        await context.SaveChangesAsync();

        await using var verification = _fixture.CreateAdminContext();
        var reloaded = await verification.Opportunities.AsNoTracking()
            .Include(o => o.Lines)
            .SingleAsync(o => o.Id == opportunity.Id);

        Assert.Equal(99.99m, reloaded.TotalAmount);
        Assert.Equal(99.99m, reloaded.Lines.Single().LineTotal);
    }
}
