using Contracts;

namespace Access.Tests.Domain;

public sealed class ActionKeyTests
{
    [Theory]
    [InlineData("crm.opportunity.win")]
    [InlineData("access.role_assignment.grant")]
    [InlineData("sales.quote.approve")]
    public void Accepts_well_formed_keys(string value)
    {
        var key = new ActionKey(value);
        Assert.Equal(value, key.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("crm")]
    [InlineData("crm.opportunity")]
    [InlineData("CRM.Opportunity.Win")]
    [InlineData("crm..win")]
    public void Rejects_malformed_keys(string value)
    {
        Assert.Throws<ArgumentException>(() => new ActionKey(value));
    }
}
