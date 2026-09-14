namespace Access.Domain.Authorization;

/// <summary>Static/seeded catalog, not a tenant row — e.g. `crm.opportunity.read`
/// (docs/schema/identity-access-schema.md §2.1).</summary>
public sealed class Permission
{
    public long Id { get; private set; }
    public string Key { get; private set; } = null!;
    public string? Description { get; private set; }

    private Permission() { }

    public static Permission Create(string key, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key is required.", nameof(key));

        return new Permission { Key = key, Description = description };
    }
}
