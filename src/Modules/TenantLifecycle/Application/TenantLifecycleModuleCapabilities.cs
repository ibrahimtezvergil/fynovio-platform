using Contracts;

namespace TenantLifecycle.Application;

public static class TenantLifecycleModuleCapabilities
{
    public const string ModuleKey = "tenant_lifecycle";
    public const int Version = 1;
    private const string SettingsSetKey = "tenant_settings_administration";

    public static readonly ModuleCapabilityManifest Manifest = new(
        ModuleKey,
        "Tenant Lifecycle",
        Version,
        PermissionSets:
        [
            new PermissionSetTemplate(SettingsSetKey, "Tenant settings administration",
            [
                new(TenantProfileActionKeys.SettingsView),
                new(TenantProfileActionKeys.SettingsUpdate)
            ])
        ],
        Roles: []);
}
