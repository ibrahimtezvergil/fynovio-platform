using Access.Domain.Authorization;
using Contracts;

namespace Access.Tests.Domain;

public sealed class PermissionSetTests
{
    [Fact]
    public void Grant_rejects_duplicate_action_key()
    {
        var set = PermissionSet.Create(new TenantId(1), "opportunity_owner", "Opportunity Owner");
        set.Grant("crm.opportunity.read", PermissionSetItem.OwnerRelation);

        Assert.Throws<InvalidOperationException>(() => set.Grant("crm.opportunity.read"));
    }

    [Fact]
    public void Grant_rejects_unsupported_relation()
    {
        var set = PermissionSet.Create(new TenantId(1), "opportunity_owner", "Opportunity Owner");
        Assert.Throws<ArgumentException>(() => set.Grant("crm.opportunity.read", "team_member"));
    }

    [Fact]
    public void Grant_with_null_relation_is_allowed()
    {
        var set = PermissionSet.Create(new TenantId(1), "opportunity_owner", "Opportunity Owner");
        var item = set.Grant("crm.opportunity.read");

        Assert.Null(item.Relation);
        Assert.Single(set.Items);
    }
}
