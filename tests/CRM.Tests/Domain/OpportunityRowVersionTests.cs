using Contracts;
using CRM.Domain;
using Xunit;

namespace CRM.Tests.Domain;

/// <summary>row_version artışı domain metotlarında olur; aynı transaction'da yazılan
/// outbox/evidence kayıtları güncel versiyonu okuyabilsin diye.</summary>
public sealed class OpportunityRowVersionTests
{
    private static Opportunity NewDraftOpportunity()
    {
        var tenant = TestData.NextTenant();
        return Opportunity.Create(tenant, new PartyRef(tenant, 1), TestData.Seller, "TRY", 1000m);
    }

    [Fact]
    public void A_new_opportunity_starts_at_version_one()
    {
        Assert.Equal(1, NewDraftOpportunity().RowVersion);
    }

    [Fact]
    public void Every_mutation_increments_the_version_once()
    {
        var opportunity = NewDraftOpportunity();

        opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 100m);
        Assert.Equal(2, opportunity.RowVersion);

        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), null, null);
        Assert.Equal(3, opportunity.RowVersion);

        opportunity.Win();
        Assert.Equal(4, opportunity.RowVersion);
    }

    [Fact]
    public void Canceling_a_line_increments_the_root_version()
    {
        var opportunity = NewDraftOpportunity();
        var line = opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 100m);
        var before = opportunity.RowVersion;

        opportunity.CancelLine(line, "stokta yok");

        Assert.Equal(before + 1, opportunity.RowVersion);
        Assert.True(line.IsCanceled);
    }

    [Fact]
    public void Canceling_a_line_of_another_opportunity_is_rejected()
    {
        var first = NewDraftOpportunity();
        var second = NewDraftOpportunity();
        var foreignLine = second.AddLine(TestData.ProductRef(second.TenantId), quantity: 1, unitPrice: 100m);

        Assert.Throws<InvalidOperationException>(() => first.CancelLine(foreignLine, "yanlış kayıt"));
    }

    [Fact]
    public void Canceling_a_line_after_completion_is_rejected()
    {
        var opportunity = NewDraftOpportunity();
        var line = opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 100m);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), null, null);
        opportunity.Win();

        Assert.Throws<InvalidOperationException>(() => opportunity.CancelLine(line, "geç kaldı"));
    }

    [Fact]
    public void A_rejected_mutation_does_not_change_the_version()
    {
        var opportunity = NewDraftOpportunity();
        var before = opportunity.RowVersion;

        Assert.Throws<InvalidOperationException>(() => opportunity.Win());

        Assert.Equal(before, opportunity.RowVersion);
    }
}
