using CRM.Application;

namespace CRM.Tests.Application;

public sealed class CrmModuleCapabilitiesTests
{
    [Fact]
    public void The_template_is_well_formed() => CrmModuleCapabilities.Manifest.Validate();

    [Fact]
    public void Every_registered_crm_action_is_available_for_tenant_authored_roles()
    {
        Assert.Equal(
            CrmActionCatalog.All.Select(action => action.ActionKey).ToHashSet(),
            CrmModuleCapabilities.Manifest.ActionKeys().ToHashSet());
        Assert.Empty(CrmModuleCapabilities.Manifest.Roles);
        Assert.DoesNotContain(CrmModuleCapabilities.Manifest.ActionKeys(), key => key.Contains('*'));
    }

    [Fact]
    public void The_catalog_still_registers_exactly_the_documented_keys_once()
    {
        var keys = CrmActionCatalog.All.Select(action => action.ActionKey).ToList();

        Assert.Equal(keys.Distinct().Count(), keys.Count);
        Assert.Contains("crm.reference.party.search", keys);
        Assert.All(keys, key => Assert.StartsWith("crm.", key));
    }
}
