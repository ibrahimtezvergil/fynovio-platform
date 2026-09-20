using CRM.Application;

namespace CRM.Tests.Application;

public sealed class CrmModuleCapabilitiesTests
{
    private static IReadOnlySet<string> ActionsOf(string roleKey)
    {
        var manifest = CrmModuleCapabilities.Manifest;
        var role = manifest.Roles.Single(r => r.Key == roleKey);
        return manifest.PermissionSets
            .Where(s => role.PermissionSetKeys.Contains(s.Key))
            .SelectMany(s => s.Items)
            .Select(i => i.ActionKey)
            .ToHashSet();
    }

    [Fact]
    public void The_template_is_well_formed() => CrmModuleCapabilities.Manifest.Validate();

    [Fact]
    public void Every_registered_crm_action_is_granted_by_some_permission_set_and_nothing_unregistered_is()
    {
        var registered = CrmActionCatalog.All.Select(a => a.ActionKey).ToHashSet();
        var templated = CrmModuleCapabilities.Manifest.ActionKeys().ToHashSet();

        Assert.Equal(registered, templated);
    }

    [Fact]
    public void The_manager_holds_every_crm_action_by_explicit_key()
    {
        Assert.Equal(CrmActionCatalog.All.Select(a => a.ActionKey).ToHashSet(), ActionsOf(CrmModuleCapabilities.ManagerRoleKey));
        Assert.DoesNotContain(CrmModuleCapabilities.Manifest.ActionKeys(), key => key.Contains('*'));
    }

    [Fact]
    public void The_viewer_can_only_read_and_list()
    {
        Assert.Equal(
            new HashSet<string> { CrmActionKeys.OpportunityRead, CrmActionKeys.OpportunityList },
            ActionsOf(CrmModuleCapabilities.ViewerRoleKey));
    }

    [Fact]
    public void The_sales_representative_works_opportunities_but_cannot_reassign()
    {
        var actions = ActionsOf(CrmModuleCapabilities.SalesRepresentativeRoleKey);

        Assert.Contains(CrmActionKeys.OpportunityChangeStage, actions);
        Assert.Contains(CrmActionKeys.PartyReferenceSearch, actions);
        Assert.DoesNotContain(CrmActionKeys.OpportunityReassign, actions);
    }

    [Fact]
    public void Only_the_manager_is_granted_to_tenant_administrators()
    {
        var granted = CrmModuleCapabilities.Manifest.Roles.Where(r => r.GrantToTenantAdministrators).Select(r => r.Key);

        Assert.Equal([CrmModuleCapabilities.ManagerRoleKey], granted);
    }

    [Fact]
    public void The_catalog_still_registers_exactly_the_documented_keys_once()
    {
        var keys = CrmActionCatalog.All.Select(a => a.ActionKey).ToList();

        Assert.Equal(keys.Distinct().Count(), keys.Count);
        Assert.Contains("crm.reference.party.search", keys);
        Assert.All(keys, key => Assert.StartsWith("crm.", key));
    }
}
