namespace TenantLifecycle.Application;

public sealed record TenantLifecycleActionDescriptor(string ActionKey, string ResourceType, string? RiskClass = null);

public static class TenantLifecycleActionCatalog
{
    public static readonly IReadOnlyList<TenantLifecycleActionDescriptor> All =
    [
        new(TenantProfileActionKeys.SettingsView, "TenantProfile"),
        new(TenantProfileActionKeys.SettingsUpdate, "TenantProfile")
    ];
}
