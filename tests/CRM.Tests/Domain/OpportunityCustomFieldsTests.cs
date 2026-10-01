using Contracts;
using CRM.Domain;

namespace CRM.Tests.Domain;

public sealed class OpportunityCustomFieldsTests
{
    private static Opportunity NewOpportunity(string? customFields = null)
    {
        var tenant = new TenantId(1);
        return Opportunity.Create(tenant, new PartyRef(tenant, 7), TestData.Seller, "TRY", 100m, customFields: customFields);
    }

    [Fact]
    public void Create_stores_the_given_custom_fields()
    {
        Assert.Equal("""{"a1":1}""", NewOpportunity("""{"a1":1}""").CustomFields);
    }

    [Fact]
    public void Replacing_custom_fields_bumps_the_version_only_when_they_change()
    {
        var opportunity = NewOpportunity();

        Assert.True(opportunity.ReplaceCustomFields("""{"a1":1}"""));
        Assert.Equal(2, opportunity.RowVersion);
        Assert.False(opportunity.ReplaceCustomFields("""{"a1":1}"""));
        Assert.Equal(2, opportunity.RowVersion);
    }

    [Fact]
    public void Closed_opportunities_can_still_change_custom_fields()
    {
        var opportunity = NewOpportunity();
        opportunity.Lose("price");

        Assert.True(opportunity.ReplaceCustomFields("""{"a1":1}"""));
    }

    [Fact]
    public void Archived_opportunities_cannot_change_custom_fields()
    {
        var opportunity = NewOpportunity();
        opportunity.Archive(confirmOpenOpportunity: false);

        Assert.Throws<InvalidOperationException>(() => opportunity.ReplaceCustomFields("""{"a1":1}"""));
    }
}
