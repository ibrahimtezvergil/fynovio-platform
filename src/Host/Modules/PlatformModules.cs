using Access.Application;
using CRM.Application;
using Collaboration.Application;
using TenantLifecycle.Application;
using Contracts;

namespace Host.Modules;

/// <summary>The one place Host lists the business modules a tenant can be given. Everything platform-wide that
/// must agree about "which modules exist" derives from here: the action registry seeded at start-up and the
/// capability templates Access copies on enablement. Adding a module means adding one line to each list —
/// and forgetting the registry line fails <c>PlatformModulesTests</c>, because the seeder DEPRECATES any
/// registered key missing from its manifest.</summary>
public static class PlatformModules
{
    public static IReadOnlyList<ModuleCapabilityManifest> CapabilityManifests { get; } = [CrmModuleCapabilities.Manifest, CollaborationModuleCapabilities.Manifest, TenantLifecycleModuleCapabilities.Manifest];

    /// <summary>The COMPLETE action registry across every module.</summary>
    public static IReadOnlyList<ActionRegistryDescriptor> ActionRegistry { get; } =
    [
        .. AccessActionCatalog.All,
        .. CrmActionCatalog.All.Select(d => new ActionRegistryDescriptor(d.ActionKey, "CRM", d.ResourceType, d.RiskClass)),
        .. CollaborationActionCatalog.All.Select(d => new ActionRegistryDescriptor(d.ActionKey, "Collaboration", d.ResourceType, d.RiskClass)),
        .. TenantLifecycleActionCatalog.All.Select(d => new ActionRegistryDescriptor(d.ActionKey, "TenantLifecycle", d.ResourceType, d.RiskClass))
    ];
}
