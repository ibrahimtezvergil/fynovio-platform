using Contracts;

namespace Access.Domain.Authorization;

/// <summary>A tenant-facing security persona (round 1 decision #7: Role is never
/// organization hierarchy). `TenantId` is NOT NULL — the old `tenant_id = null` "system
/// role" model is retired; a platform-template-provisioned row is still a real,
/// tenant-owned row with `Origin = OriginSystemTemplate` (round 4 Decision A,
/// gap-closure §6).</summary>
public sealed class Role
{
    public const string OriginTenant = "tenant";
    public const string OriginSystemTemplate = "system_template";

    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Key { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Origin { get; private set; } = null!;

    /// <summary>Provenance of a template copy: which module's manifest, at which version, produced this row.
    /// Both are set together and only for `system_template` rows; informational — nothing reconciles the row
    /// against a newer template (Phase 1.5 Decision A).</summary>
    public string? OriginModuleKey { get; private set; }
    public int? OriginVersion { get; private set; }

    private Role() { }

    public static Role Create(
        TenantId tenantId, string key, string name, string origin = OriginTenant,
        string? originModuleKey = null, int? originVersion = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key is required.", nameof(key));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (origin is not (OriginTenant or OriginSystemTemplate))
            throw new ArgumentException("Origin must be 'tenant' or 'system_template'.", nameof(origin));
        TemplateProvenance.Validate(origin, originModuleKey, originVersion, OriginSystemTemplate);

        return new Role
        {
            TenantId = tenantId,
            Key = key,
            Name = name,
            Origin = origin,
            OriginModuleKey = originModuleKey,
            OriginVersion = originVersion
        };
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        Name = name;
    }
}
