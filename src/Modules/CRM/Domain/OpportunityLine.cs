using Contracts;

namespace CRM.Domain;

public sealed class OpportunityLine
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public long OpportunityId { get; private set; }
    public string ProductRefBoundedContext { get; private set; } = null!;
    public string ProductRefEntityType { get; private set; } = null!;
    public long ProductRefId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal? LineTotal { get; private set; }
    public bool IsOptional { get; private set; }
    public bool IsCanceled { get; private set; }
    public string? CancelReason { get; private set; }
    public int SortOrder { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Master Data doesn't own real product rows yet; no FK, per doc 07 §3
    /// (no cross-schema foreign keys) — see docs/schema/crm-sales-schema.md.</summary>
    public EntityRef ProductRef => new(TenantId, ProductRefBoundedContext, ProductRefEntityType, ProductRefId);

    private OpportunityLine() { }

    internal static OpportunityLine Create(
        TenantId tenantId,
        EntityRef productRef,
        int quantity,
        decimal unitPrice,
        bool isOptional,
        int sortOrder)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        if (unitPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");

        return new OpportunityLine
        {
            TenantId = tenantId,
            ProductRefBoundedContext = productRef.BoundedContext,
            ProductRefEntityType = productRef.EntityType,
            ProductRefId = productRef.Id,
            Quantity = quantity,
            UnitPrice = unitPrice,
            IsOptional = isOptional,
            SortOrder = sortOrder,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Cancel(string cancelReason)
    {
        if (string.IsNullOrWhiteSpace(cancelReason))
            throw new ArgumentException("Cancel reason is required.", nameof(cancelReason));

        IsCanceled = true;
        CancelReason = cancelReason;
    }
}
