using CRM.Domain;
using Xunit;

namespace CRM.Tests.Domain;

public sealed class OpportunityStateMachineTests
{
    private static Opportunity NewWaitingOpportunity()
    {
        var tenant = TestData.NextTenant();
        return Opportunity.Create(tenant, partyId: 1, TestData.Seller, "TRY", estimatedAmount: 1000m);
    }

    [Fact]
    public void Create_starts_in_waiting()
    {
        var opportunity = NewWaitingOpportunity();

        Assert.Equal(OpportunityStatus.Waiting, opportunity.Status);
        Assert.Null(opportunity.ExpiryDate);
    }

    [Fact]
    public void Create_rejects_a_currency_that_is_not_three_letters()
    {
        var tenant = TestData.NextTenant();

        Assert.Throws<ArgumentException>(() =>
            Opportunity.Create(tenant, partyId: 1, TestData.Seller, "TRYX", 1000m));
    }

    [Fact]
    public void Offer_requires_a_future_expiry_date()
    {
        var opportunity = NewWaitingOpportunity();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            opportunity.Offer(DateTimeOffset.UtcNow.AddDays(-1)));
    }

    [Fact]
    public void Offer_moves_to_offered_and_stamps_offer_date()
    {
        var opportunity = NewWaitingOpportunity();

        opportunity.Offer(DateTimeOffset.UtcNow.AddDays(7));

        Assert.Equal(OpportunityStatus.Offered, opportunity.Status);
        Assert.NotNull(opportunity.OfferDate);
        Assert.NotNull(opportunity.ExpiryDate);
    }

    [Fact]
    public void Complete_is_rejected_while_waiting()
    {
        var opportunity = NewWaitingOpportunity();

        Assert.Throws<InvalidOperationException>(() => opportunity.Complete(1000m));
    }

    [Fact]
    public void Complete_is_rejected_without_an_active_required_line()
    {
        var opportunity = NewWaitingOpportunity();
        opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 100m, isOptional: true);
        opportunity.Offer(DateTimeOffset.UtcNow.AddDays(7));

        Assert.Throws<InvalidOperationException>(() => opportunity.Complete(100m));
    }

    [Fact]
    public void Complete_succeeds_with_one_active_required_line()
    {
        var opportunity = NewWaitingOpportunity();
        opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 2, unitPrice: 50m);
        opportunity.Offer(DateTimeOffset.UtcNow.AddDays(7));

        opportunity.Complete(100m);

        Assert.Equal(OpportunityStatus.Completed, opportunity.Status);
        Assert.NotNull(opportunity.SaleDate);
    }

    [Fact]
    public void Cancel_requires_a_reason()
    {
        var opportunity = NewWaitingOpportunity();

        Assert.Throws<ArgumentException>(() => opportunity.Cancel("  "));
    }

    [Fact]
    public void Cancel_from_waiting_is_allowed()
    {
        var opportunity = NewWaitingOpportunity();

        opportunity.Cancel("müşteri vazgeçti");

        Assert.Equal(OpportunityStatus.Canceled, opportunity.Status);
        Assert.NotNull(opportunity.CancelDate);
    }

    [Fact]
    public void AddLine_is_rejected_after_offer()
    {
        var opportunity = NewWaitingOpportunity();
        opportunity.Offer(DateTimeOffset.UtcNow.AddDays(7));

        Assert.Throws<InvalidOperationException>(() =>
            opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 10m));
    }
}
