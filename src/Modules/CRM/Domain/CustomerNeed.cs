using Contracts;

namespace CRM.Domain;

public sealed class CustomerNeed
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Category { get; private set; }
    public ConfigurationStatus Status { get; private set; }
    public long RowVersion { get; private set; } = 1;
    public decimal AveragePrice { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private CustomerNeed() { }

    public static CustomerNeed Create(TenantId tenantId, string name, decimal averagePrice, string? category = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (averagePrice < 0)
            throw new ArgumentOutOfRangeException(nameof(averagePrice), "Average price cannot be negative.");

        return new CustomerNeed
        {
            TenantId = tenantId,
            Name = name,
            Category = category,
            Status = ConfigurationStatus.Active,
            AveragePrice = averagePrice,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Replace(string name, string? category, decimal averagePrice)
    {
        if (Status == ConfigurationStatus.Archived) throw new InvalidOperationException("An archived customer need cannot be edited.");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) throw new ArgumentException("A customer need name of 1–100 characters is required.", nameof(name));
        if (category is { Length: > 100 } || averagePrice < 0 || decimal.Round(averagePrice, 2) != averagePrice) throw new ArgumentException("Customer need category or average price is invalid.");
        Name = name.Trim();
        Category = category?.Trim();
        AveragePrice = averagePrice;
        RowVersion++;
    }

    public void ChangeStatus(ConfigurationStatus status)
    {
        if (Status == ConfigurationStatus.Archived && status != ConfigurationStatus.Archived) throw new InvalidOperationException("An archived customer need cannot be reactivated.");
        Status = status;
        RowVersion++;
    }
}
