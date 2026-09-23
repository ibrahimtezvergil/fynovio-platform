using Contracts;

namespace Access.Domain.Authorization;

/// <summary>Reusable business-capability bundle — the first-class abstraction between
/// `Role` and `ActionKey` (round 1 decision #6). `Origin` records whether this row was
/// provisioned from a platform template or authored directly by the tenant
/// (round 4 Decision A) — it does not imply a reconciler exists; there is none in
/// Phase 1.5 (gap-closure §6).</summary>
public sealed class PermissionSet
{
    public const string OriginTenant = "tenant";
    public const string OriginSystemTemplate = "system_template";

    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Key { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Origin { get; private set; } = null!;

    /// <summary>See <see cref="Role.OriginModuleKey"/>.</summary>
    public string? OriginModuleKey { get; private set; }
    public int? OriginVersion { get; private set; }

    private readonly List<PermissionSetItem> _items = [];
    public IReadOnlyCollection<PermissionSetItem> Items => _items;

    private PermissionSet() { }

    public static PermissionSet Create(
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

        return new PermissionSet
        {
            TenantId = tenantId,
            Key = key,
            Name = name,
            Origin = origin,
            OriginModuleKey = originModuleKey,
            OriginVersion = originVersion
        };
    }

    public PermissionSetItem Grant(string actionKey, string? relation = null)
    {
        if (_items.Any(i => i.ActionKey == actionKey))
            throw new InvalidOperationException($"'{actionKey}' is already granted by permission set '{Key}'.");

        var item = PermissionSetItem.Create(TenantId, actionKey, relation);
        _items.Add(item);
        return item;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        Name = name;
    }

    public void ReplaceGrants(IEnumerable<(string ActionKey, string? Relation)> grants)
    {
        var values = grants.ToArray();
        if (values.Length == 0)
            throw new ArgumentException("At least one action is required.", nameof(grants));
        if (values.Select(value => value.ActionKey).Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new ArgumentException("An action can only be granted once.", nameof(grants));

        _items.Clear();
        foreach (var (actionKey, relation) in values)
            Grant(actionKey, relation);
    }
}
