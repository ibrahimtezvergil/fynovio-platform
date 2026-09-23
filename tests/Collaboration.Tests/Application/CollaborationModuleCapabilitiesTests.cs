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

    [Fact]
    public void The_template_is_well_formed() => CollaborationModuleCapabilities.Manifest.Validate();

    [Fact]
    public void Every_registered_action_is_available_for_tenant_authored_roles()
    {
        Assert.Equal(ExpectedKeys, CollaborationModuleCapabilities.Manifest.ActionKeys().ToHashSet());
        Assert.Empty(CollaborationModuleCapabilities.Manifest.Roles);
    }

    [Fact]
    public void Every_action_is_explicit_and_uses_the_owner_relation()
    {
        var items = CollaborationModuleCapabilities.Manifest.PermissionSets.SelectMany(set => set.Items).ToList();

        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.Equal(ModuleCapabilityManifest.OwnerRelation, item.Relation));
        Assert.DoesNotContain(items, item => item.ActionKey.Contains('*'));
    }

    [Theory]
    [InlineData(typeof(CreateCalendarEntryHandler), CollaborationActionKeys.CalendarEntryCreate)]
    [InlineData(typeof(GetCalendarEntryHandler), CollaborationActionKeys.CalendarEntryRead)]
    [InlineData(typeof(ListCalendarEntriesHandler), CollaborationActionKeys.CalendarEntryList)]
    [InlineData(typeof(UpdateCalendarEntryHandler), CollaborationActionKeys.CalendarEntryUpdate)]
    [InlineData(typeof(DeleteCalendarEntryHandler), CollaborationActionKeys.CalendarEntryDelete)]
    public void A_handlers_private_action_key_constant_equals_the_catalog_key(Type handler, string expected)
    {
        var field = handler.GetField("ActionKeyValue", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        Assert.Equal(expected, (string?)field.GetRawConstantValue());
    }
}
