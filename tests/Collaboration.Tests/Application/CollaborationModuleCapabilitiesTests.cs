using System.Reflection;
using Collaboration.Application;
using Contracts;

namespace Collaboration.Tests.Application;

public sealed class CollaborationModuleCapabilitiesTests
{
    private static readonly HashSet<string> ExpectedKeys =
    [
        "collaboration.calendar_entry.create",
        "collaboration.calendar_entry.read",
        "collaboration.calendar_entry.list",
        "collaboration.calendar_entry.update",
        "collaboration.calendar_entry.delete"
    ];

    private static IReadOnlySet<string> ActionsOf(string roleKey)
    {
        var manifest = CollaborationModuleCapabilities.Manifest;
        var role = manifest.Roles.Single(r => r.Key == roleKey);
        return manifest.PermissionSets
            .Where(s => role.PermissionSetKeys.Contains(s.Key))
            .SelectMany(s => s.Items)
            .Select(i => i.ActionKey)
            .ToHashSet();
    }

    [Fact]
    public void The_template_is_well_formed() => CollaborationModuleCapabilities.Manifest.Validate();

    [Fact]
    public void The_manifest_identifies_the_collaboration_module_at_version_one()
    {
        Assert.Equal("collaboration", CollaborationModuleCapabilities.Manifest.ModuleKey);
        Assert.Equal(1, CollaborationModuleCapabilities.Manifest.Version);
    }

    [Fact]
    public void Every_registered_action_is_granted_by_some_permission_set_and_nothing_unregistered_is()
    {
        var registered = CollaborationActionCatalog.All.Select(a => a.ActionKey).ToHashSet();
        var templated = CollaborationModuleCapabilities.Manifest.ActionKeys().ToHashSet();

        Assert.Equal(registered, templated);
    }

    [Fact]
    public void The_catalog_registers_exactly_the_documented_keys_once()
    {
        var keys = CollaborationActionCatalog.All.Select(a => a.ActionKey).ToList();

        Assert.Equal(keys.Distinct().Count(), keys.Count);
        Assert.Equal(ExpectedKeys, keys.ToHashSet());
        Assert.All(keys, key => Assert.StartsWith("collaboration.", key));
    }

    [Fact]
    public void The_action_key_constants_match_the_catalog()
    {
        var constants = typeof(CollaborationActionKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet();

        Assert.Equal(constants, CollaborationActionCatalog.All.Select(a => a.ActionKey).ToHashSet());
    }

    [Fact]
    public void No_collaboration_action_is_risk_catalogued()
    {
        // Personal calendar notes are not on the risk catalog, so no action may claim a risk class (and with it
        // an evidence requirement) it does not have.
        Assert.All(CollaborationActionCatalog.All, descriptor => Assert.Null(descriptor.RiskClass));
    }

    [Fact]
    public void Every_action_is_granted_through_the_owner_relation_by_explicit_key()
    {
        var items = CollaborationModuleCapabilities.Manifest.PermissionSets.SelectMany(s => s.Items).ToList();

        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.Equal(ModuleCapabilityManifest.OwnerRelation, item.Relation));
        Assert.DoesNotContain(items, item => item.ActionKey.Contains('*'));
    }

    [Fact]
    public void The_collaboration_user_holds_every_calendar_action()
    {
        Assert.Equal(ExpectedKeys, ActionsOf(CollaborationModuleCapabilities.UserRoleKey));
    }

    [Fact]
    public void The_collaboration_user_is_the_only_role_and_is_granted_to_tenant_administrators()
    {
        var manifest = CollaborationModuleCapabilities.Manifest;

        Assert.Equal([CollaborationModuleCapabilities.UserRoleKey], manifest.Roles.Select(r => r.Key));
        Assert.Equal([CollaborationModuleCapabilities.UserRoleKey], manifest.Roles.Where(r => r.GrantToTenantAdministrators).Select(r => r.Key));
    }

    [Theory]
    [InlineData(typeof(CreateCalendarEntryHandler), CollaborationActionKeys.CalendarEntryCreate)]
    [InlineData(typeof(GetCalendarEntryHandler), CollaborationActionKeys.CalendarEntryRead)]
    [InlineData(typeof(ListCalendarEntriesHandler), CollaborationActionKeys.CalendarEntryList)]
    public void A_handlers_private_action_key_constant_equals_the_catalog_key(Type handler, string expected)
    {
        var field = handler.GetField("ActionKeyValue", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        Assert.Equal(expected, (string?)field.GetRawConstantValue());
        Assert.Contains(expected, CollaborationActionCatalog.All.Select(a => a.ActionKey));
    }
}
