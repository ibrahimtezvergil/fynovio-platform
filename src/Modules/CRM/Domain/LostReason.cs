using Contracts;

namespace CRM.Domain;

public sealed class LostReason
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Key { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public ConfigurationStatus Status { get; private set; }
    public long RowVersion { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    private LostReason() { }
    public static LostReason Create(TenantId tenantId, string key, string name) => new()
    { TenantId = tenantId, Key = Required(key, nameof(key)), Name = Required(name, nameof(name)), Status = ConfigurationStatus.Active, CreatedAt = DateTimeOffset.UtcNow };
    public void Rename(string name)
    {
        if (Status == ConfigurationStatus.Archived) throw new InvalidOperationException("An archived lost reason cannot be edited.");
        Name = Required(name, nameof(name));
        RowVersion++;
    }
    public void ChangeStatus(ConfigurationStatus status)
    {
        if (Status == ConfigurationStatus.Archived && status != ConfigurationStatus.Archived) throw new InvalidOperationException("An archived lost reason cannot be reactivated.");
        Status = status;
        RowVersion++;
    }
    private static string Required(string value, string name) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > 100 ? throw new ArgumentException("A non-empty value of at most 100 characters is required.", name) : value.Trim();
}
