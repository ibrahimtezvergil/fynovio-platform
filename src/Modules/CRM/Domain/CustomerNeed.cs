using Contracts;

namespace CRM.Domain;

public sealed class CustomerNeed
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public decimal AveragePrice { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private CustomerNeed() { }

    public static CustomerNeed Create(TenantId tenantId, string name, decimal averagePrice)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (averagePrice < 0)
            throw new ArgumentOutOfRangeException(nameof(averagePrice), "Average price cannot be negative.");

        return new CustomerNeed
        {
            TenantId = tenantId,
            Name = name,
            AveragePrice = averagePrice,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
