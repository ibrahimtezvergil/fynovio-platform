using Contracts;
using CRM.Domain;
using Xunit;

namespace CRM.Tests.Domain;

/// <summary>docs/schema/crm-sales-schema.md "Money: one rounding rule": girilen değerler
/// 2 hane, hesaplanan değerler 4 hane; toplam satırlardan türetilir ve `won`
/// geçişinde tam olarak bir kez 2 haneye yuvarlanır.</summary>
public sealed class OpportunityMoneyTests
{
    private static Opportunity DraftOpportunity()
    {
        var tenant = TestData.NextTenant();
        return Opportunity.Create(tenant, new PartyRef(tenant, 1), TestData.Seller, "TRY", 0m);
    }

    [Fact]
    public void AddLine_computes_the_line_total()
    {
        var opportunity = DraftOpportunity();

        var line = opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 3, unitPrice: 33.33m);

        Assert.Equal(99.99m, line.LineTotal);
    }

    [Fact]
    public void AddLine_rejects_a_unit_price_with_more_than_two_decimals()
    {
        var opportunity = DraftOpportunity();

        Assert.Throws<ArgumentException>(() =>
            opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 33.335m));
    }

    [Fact]
    public void Create_rejects_an_estimated_amount_with_more_than_two_decimals()
    {
        var tenant = TestData.NextTenant();
        Assert.Throws<ArgumentException>(() =>
            Opportunity.Create(tenant, new PartyRef(tenant, 1), TestData.Seller, "TRY", 10.001m));
    }

    [Fact]
    public void Win_derives_the_total_from_active_required_lines()
    {
        var opportunity = DraftOpportunity();
        opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 2, unitPrice: 50m);
        opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 25.50m);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), null, null);

        opportunity.Win();

        Assert.Equal(125.50m, opportunity.TotalAmount);
    }

    [Fact]
    public void Win_excludes_optional_lines_from_the_total()
    {
        var opportunity = DraftOpportunity();
        opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 100m);
        opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 40m, isOptional: true);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), null, null);

        opportunity.Win();

        Assert.Equal(100.00m, opportunity.TotalAmount);
    }

    [Fact]
    public void Win_excludes_canceled_lines_from_the_total()
    {
        var opportunity = DraftOpportunity();
        opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 100m);
        var canceled = opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 40m);
        opportunity.CancelLine(canceled, "stokta yok");
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), null, null);

        opportunity.Win();

        Assert.Equal(100.00m, opportunity.TotalAmount);
    }
}
