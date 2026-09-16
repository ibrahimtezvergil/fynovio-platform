using CRM.Domain;
using Xunit;

namespace CRM.Tests.Domain;

public sealed class OpportunityStateMachineTests
{
    private static Opportunity NewDraftOpportunity()
    {
        var tenant = TestData.NextTenant();
        return Opportunity.Create(tenant, partyId: 1, TestData.Seller, "TRY", estimatedAmount: 1000m);
    }

    [Fact]
    public void Create_starts_in_draft()
    {
        var opportunity = NewDraftOpportunity();

        Assert.Equal(OpportunityStatus.Draft, opportunity.Status);
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
    public void Open_requires_a_future_expiry_date()
    {
        var opportunity = NewDraftOpportunity();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            opportunity.Open(DateTimeOffset.UtcNow.AddDays(-1)));
    }

    [Fact]
    public void Open_moves_to_open_and_stamps_opened_date()
    {
        var opportunity = NewDraftOpportunity();

        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7));

        Assert.Equal(OpportunityStatus.Open, opportunity.Status);
        Assert.NotNull(opportunity.OpenedDate);
        Assert.NotNull(opportunity.ExpiryDate);
    }

    [Fact]
    public void Win_is_rejected_while_draft()
    {
        var opportunity = NewDraftOpportunity();

        Assert.Throws<InvalidOperationException>(() => opportunity.Win());
    }

    [Fact]
    public void Win_is_rejected_without_an_active_required_line()
    {
        var opportunity = NewDraftOpportunity();
        opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 100m, isOptional: true);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7));

        Assert.Throws<InvalidOperationException>(() => opportunity.Win());
    }

    [Fact]
    public void Win_succeeds_with_one_active_required_line()
    {
        var opportunity = NewDraftOpportunity();
        opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 2, unitPrice: 50m);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7));

        opportunity.Win();

        Assert.Equal(OpportunityStatus.Won, opportunity.Status);
        Assert.NotNull(opportunity.WonDate);
    }

    [Fact]
    public void Lose_requires_a_reason()
    {
        var opportunity = NewDraftOpportunity();

        Assert.Throws<ArgumentException>(() => opportunity.Lose("  "));
    }

    [Fact]
    public void Lose_from_draft_is_allowed()
    {
        var opportunity = NewDraftOpportunity();

        opportunity.Lose("müşteri vazgeçti");

        Assert.Equal(OpportunityStatus.Lost, opportunity.Status);
        Assert.NotNull(opportunity.LostDate);
    }

    [Fact]
    public void AddLine_is_rejected_after_open()
    {
        var opportunity = NewDraftOpportunity();
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7));

        Assert.Throws<InvalidOperationException>(() =>
            opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 10m));
    }
}
