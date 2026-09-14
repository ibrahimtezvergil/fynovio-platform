using Contracts;

namespace Access.Domain.Authorization;

/// <summary>`tenant_id = null` means a platform system role (docs/schema/identity-access-schema.md
/// §2.1). Permission catalog itself is static/seeded, not a tenant row — see
/// <see cref="Permission"/>.</summary>
public sealed class Role
{
    public long Id { get; private set; }
    public TenantId? TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public bool IsSystem { get; private set; }

    private Role() { }

    public static Role Create(string name, TenantId? tenantId = null, bool isSystem = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (tenantId is not null && isSystem)
            throw new ArgumentException("A system role cannot be tenant-scoped.", nameof(isSystem));

        return new Role { Name = name, TenantId = tenantId, IsSystem = isSystem };
    }
}
