using Contracts;

namespace Collaboration.Application;

public static class CollaborationModuleCapabilities
{
    public const string ModuleKey = "collaboration";
    public const int Version = 1;
    public const string UserRoleKey = "collaboration_user";
    private const string CalendarSetKey = "collaboration_calendar_entry_owner";

    public static readonly ModuleCapabilityManifest Manifest = new(
        ModuleKey,
        "Collaboration",
        Version,
        PermissionSets:
        [
            new PermissionSetTemplate(CalendarSetKey, "Collaboration — own calendar entries",
            [
                new(CollaborationActionKeys.CalendarEntryCreate, "owner"),
                new(CollaborationActionKeys.CalendarEntryRead, "owner"),
                new(CollaborationActionKeys.CalendarEntryList, "owner"),
                new(CollaborationActionKeys.CalendarEntryUpdate, "owner"),
                new(CollaborationActionKeys.CalendarEntryDelete, "owner")
            ])
        ],
        Roles: [new RoleTemplate(UserRoleKey, "Collaboration User", [CalendarSetKey], GrantToTenantAdministrators: true)]);
}
