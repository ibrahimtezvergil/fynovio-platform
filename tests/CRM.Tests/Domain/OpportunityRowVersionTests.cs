using CRM.Domain;
using Xunit;

namespace CRM.Tests.Domain;

/// <summary>row_version artışı domain metotlarında olur; aynı transaction'da yazılan
/// outbox/evidence kayıtları güncel versiyonu okuyabilsin diye.</summary>
public sealed class OpportunityRowVersionTests
{
    private static Opportunity NewWaitingOpportunity() =>
        Opportunity.Create(TestData.NextTenant(), partyId: 1, TestData.Seller, "TRY", 1000m);

    [Fact]
    public void A_new_opportunity_starts_at_version_one()
    {
        Assert.Equal(1, NewWaitingOpportunity().RowVersion);
    }

    [Fact]
    public void Every_mutation_increments_the_version_once()
    {
        var opportunity = NewWaitingOpportunity();

        opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 100m);
        Assert.Equal(2, opportunity.RowVersion);

        opportunity.Offer(DateTimeOffset.UtcNow.AddDays(7));
        Assert.Equal(3, opportunity.RowVersion);

        opportunity.Complete(100m);
        Assert.Equal(4, opportunity.RowVersion);
    }

    [Fact]
    public void Canceling_a_line_increments_the_root_version()
    {
        var opportunity = NewWaitingOpportunity();
        var line = opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 100m);
        var before = opportunity.RowVersion;

        opportunity.CancelLine(line, "stokta yok");

        Assert.Equal(before + 1, opportunity.RowVersion);
        Assert.True(line.IsCanceled);
    }

    [Fact]
    public void Canceling_a_line_of_another_opportunity_is_rejected()
    {
        var first = NewWaitingOpportunity();
        var second = NewWaitingOpportunity();
        var foreignLine = second.AddLine(TestData.ProductRef(second.TenantId), quantity: 1, unitPrice: 100m);

        Assert.Throws<InvalidOperationException>(() => first.CancelLine(foreignLine, "yanlış kayıt"));
    }

    [Fact]
    public void Canceling_a_line_after_completion_is_rejected()
    {
        var opportunity = NewWaitingOpportunity();
        var line = opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 100m);
        opportunity.Offer(DateTimeOffset.UtcNow.AddDays(7));
        opportunity.Complete(100m);

        Assert.Throws<InvalidOperationException>(() => opportunity.CancelLine(line, "geç kaldı"));
    }

    [Fact]
    public void A_rejected_mutation_does_not_change_the_version()
    {
        var opportunity = NewWaitingOpportunity();
        var before = opportunity.RowVersion;

        Assert.Throws<InvalidOperationException>(() => opportunity.Complete(100m));

        Assert.Equal(before, opportunity.RowVersion);
    }
}
