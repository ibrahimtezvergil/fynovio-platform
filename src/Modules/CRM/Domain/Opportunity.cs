using Contracts;

namespace CRM.Domain;

public enum OpportunityStatus
{
    Waiting,
    Offered,
    Completed,
    Canceled
}

/// <summary>Aggregate root for the merged CRM+Sales pilot domain
/// (docs/architecture-analysis/17_CRM_SALES_PILOT_DOMAIN.md §2). The database enforces
/// single-row state consistency (status/date CHECK constraints); this aggregate enforces
/// the cross-row invariant that CHECK cannot express — see
/// docs/schema/crm-sales-schema.md, "Domain-vs-database invariant boundary".</summary>
public sealed class Opportunity : IHasRowVersion
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public long PartyId { get; private set; }
    public string AssignedPrincipalIssuer { get; private set; } = null!;
    public string AssignedPrincipalSubject { get; private set; } = null!;
    public OpportunityStatus Status { get; private set; }
    public string? CancelReason { get; private set; }
    public string Currency { get; private set; } = null!;
    public decimal EstimatedAmount { get; private set; }
    public decimal? TotalAmount { get; private set; }
    public DateTimeOffset? ExpiryDate { get; private set; }
    public DateTimeOffset? OfferDate { get; private set; }
    public DateTimeOffset? SaleDate { get; private set; }
    public DateTimeOffset? CancelDate { get; private set; }
    public string? CustomFields { get; private set; }
    public long RowVersion { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<OpportunityLine> _lines = [];
    public IReadOnlyCollection<OpportunityLine> Lines => _lines;

    public PrincipalRef AssignedPrincipal => new(AssignedPrincipalIssuer, AssignedPrincipalSubject);

    private Opportunity() { }

    public static Opportunity Create(
        TenantId tenantId,
        long partyId,
        PrincipalRef assignedPrincipal,
        string currency,
        decimal estimatedAmount)
    {
        if (currency is not { Length: 3 })
            throw new ArgumentException("Currency must be a 3-letter ISO code.", nameof(currency));
        if (estimatedAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(estimatedAmount), "Estimated amount cannot be negative.");

        var now = DateTimeOffset.UtcNow;
        return new Opportunity
        {
            TenantId = tenantId,
            PartyId = partyId,
            AssignedPrincipalIssuer = assignedPrincipal.Issuer,
            AssignedPrincipalSubject = assignedPrincipal.Subject,
            Status = OpportunityStatus.Waiting,
            Currency = currency,
            EstimatedAmount = estimatedAmount,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public OpportunityLine AddLine(EntityRef productRef, int quantity, decimal unitPrice, bool isOptional = false, int sortOrder = 0)
    {
        if (Status != OpportunityStatus.Waiting)
            throw new InvalidOperationException($"Cannot add a line to an opportunity in status {Status}.");

        var line = OpportunityLine.Create(TenantId, productRef, quantity, unitPrice, isOptional, sortOrder);
        _lines.Add(line);
        Touch();
        return line;
    }

    /// <summary>waiting → offered is a DB-enforced gate (17 §2): expiry_date becomes
    /// mandatory from this point on, diverging from legacy's skip-behavior on purpose.</summary>
    public void Offer(DateTimeOffset expiryDate)
    {
        if (Status != OpportunityStatus.Waiting)
            throw new InvalidOperationException($"Cannot offer an opportunity in status {Status}.");
        if (expiryDate <= DateTimeOffset.UtcNow)
            throw new ArgumentOutOfRangeException(nameof(expiryDate), "Expiry date must be in the future.");

        Status = OpportunityStatus.Offered;
        ExpiryDate = expiryDate;
        OfferDate = DateTimeOffset.UtcNow;
        Touch();
    }

    /// <summary>Enforces the one cross-row invariant CHECK cannot express: at least one
    /// active (non-canceled), required (non-optional) line must exist.</summary>
    public void Complete(decimal totalAmount)
    {
        if (Status != OpportunityStatus.Offered)
            throw new InvalidOperationException($"Cannot complete an opportunity in status {Status}. It must be offered first.");
        if (totalAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(totalAmount), "Total amount cannot be negative.");
        if (!_lines.Any(line => !line.IsOptional && !line.IsCanceled))
            throw new InvalidOperationException("Cannot complete an opportunity without at least one active required line.");

        Status = OpportunityStatus.Completed;
        // Single declared rounding point (17 §3.4): 4dp computed total rounds to the
        // currency's 2dp minor unit exactly once, here.
        TotalAmount = Math.Round(totalAmount, 2, MidpointRounding.AwayFromZero);
        SaleDate = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Cancel(string cancelReason)
    {
        if (Status is OpportunityStatus.Completed or OpportunityStatus.Canceled)
            throw new InvalidOperationException($"Cannot cancel an opportunity in status {Status}.");
        if (string.IsNullOrWhiteSpace(cancelReason))
            throw new ArgumentException("Cancel reason is required.", nameof(cancelReason));

        Status = OpportunityStatus.Canceled;
        CancelReason = cancelReason;
        CancelDate = DateTimeOffset.UtcNow;
        Touch();
    }

    public void CancelLine(OpportunityLine line, string cancelReason)
    {
        if (!_lines.Contains(line))
            throw new InvalidOperationException("Line does not belong to this opportunity.");
        if (Status is OpportunityStatus.Completed or OpportunityStatus.Canceled)
            throw new InvalidOperationException($"Cannot cancel a line on an opportunity in status {Status}.");

        line.Cancel(cancelReason);
        Touch();
    }

    /// <summary>Tek versiyon artış noktası. Interceptor yerine burada artırılıyor: outbox ve
    /// evidence kayıtları aynı transaction içinde `RowVersion`'ı okuyor, interceptor
    /// SaveChanges sırasında artırdığı için bir eski değer yazılıyordu.</summary>
    private void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        RowVersion++;
    }

    void IHasRowVersion.IncrementRowVersion() => RowVersion++;
}
