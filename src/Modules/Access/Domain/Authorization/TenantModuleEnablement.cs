using Contracts;

namespace Access.Domain.Authorization;

/// <summary>The record that a tenant was given a module's capability template at a specific version. The row
/// is written once, in the same transaction as the role/permission-set copies it describes, and never
/// updated: enablement is not a subscription to future template changes (Phase 1.5 Decision A — no reconciler).</summary>
public sealed class TenantModuleEnablement
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string ModuleKey { get; private set; } = null!;
    public int TemplateVersion { get; private set; }
    public DateTimeOffset EnabledAt { get; private set; }

    private TenantModuleEnablement() { }

    public static TenantModuleEnablement Enable(TenantId tenantId, string moduleKey, int templateVersion, DateTimeOffset enabledAt)
    {
        if (string.IsNullOrWhiteSpace(moduleKey))
            throw new ArgumentException("Module key is required.", nameof(moduleKey));
        if (templateVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(templateVersion), "Template version must be at least 1.");

        return new TenantModuleEnablement { TenantId = tenantId, ModuleKey = moduleKey, TemplateVersion = templateVersion, EnabledAt = enabledAt };
    }
}
