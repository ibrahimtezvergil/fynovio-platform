using Contracts;

namespace CRM.Domain;

public enum OpportunityStatus
{
    Draft,
    Open,
    Won,
    Lost
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
    public long PartyRefPartyId { get; private set; }
    public long? PipelineDefinitionVersionId { get; private set; }
    public long? PipelineStageId { get; private set; }
    public string AssignedPrincipalIssuer { get; private set; } = null!;
    public string AssignedPrincipalSubject { get; private set; } = null!;
    public OpportunityStatus Status { get; private set; }
    public string? LostReason { get; private set; }
    public string Currency { get; private set; } = null!;
    public decimal EstimatedAmount { get; private set; }
    public decimal? TotalAmount { get; private set; }
    public DateTimeOffset? ExpiryDate { get; private set; }
    public DateTimeOffset? OpenedDate { get; private set; }
    public DateTimeOffset? WonDate { get; private set; }
    public DateTimeOffset? LostDate { get; private set; }
    public string? CustomFields { get; private set; }
    public long RowVersion { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<OpportunityLine> _lines = [];
    public IReadOnlyCollection<OpportunityLine> Lines => _lines;

    public PrincipalRef AssignedPrincipal => new(AssignedPrincipalIssuer, AssignedPrincipalSubject);
    public PartyRef PartyRef => new(TenantId, PartyRefPartyId);

    private Opportunity() { }

    public static Opportunity Create(
        TenantId tenantId,
        PartyRef partyRef,
        PrincipalRef assignedPrincipal,
        string currency,
        decimal estimatedAmount)
    {
        if (partyRef.TenantId != tenantId)
            throw new ArgumentException("PartyRef's tenant must match the opportunity's tenant.", nameof(partyRef));
        if (currency is not { Length: 3 })
            throw new ArgumentException("Currency must be a 3-letter ISO code.", nameof(currency));
        if (estimatedAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(estimatedAmount), "Estimated amount cannot be negative.");
        if (decimal.Round(estimatedAmount, 2) != estimatedAmount)
            throw new ArgumentException("Estimated amount is an entered value and must have at most two decimal places.", nameof(estimatedAmount));

        var now = DateTimeOffset.UtcNow;
        return new Opportunity
        {
            TenantId = tenantId,
            PartyRefPartyId = partyRef.PartyId,
            AssignedPrincipalIssuer = assignedPrincipal.Issuer,
            AssignedPrincipalSubject = assignedPrincipal.Subject,
            Status = OpportunityStatus.Draft,
            Currency = currency,
            EstimatedAmount = estimatedAmount,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public OpportunityLine AddLine(EntityRef productRef, int quantity, decimal unitPrice, bool isOptional = false, int sortOrder = 0)
    {
        if (Status != OpportunityStatus.Draft)
            throw new InvalidOperationException($"Cannot add a line to an opportunity in status {Status}.");

        var line = OpportunityLine.Create(TenantId, productRef, quantity, unitPrice, isOptional, sortOrder);
        _lines.Add(line);
        Touch();
        return line;
    }

    /// <summary>draft → open is a DB-enforced gate (17 §2): expiry_date becomes
    /// mandatory from this point on, diverging from legacy's skip-behavior on purpose.</summary>
    public void Open(DateTimeOffset expiryDate)
    {
        if (Status != OpportunityStatus.Draft)
            throw new InvalidOperationException($"Cannot open an opportunity in status {Status}.");
        if (expiryDate <= DateTimeOffset.UtcNow)
            throw new ArgumentOutOfRangeException(nameof(expiryDate), "Expiry date must be in the future.");

        Status = OpportunityStatus.Open;
        ExpiryDate = expiryDate;
        OpenedDate = DateTimeOffset.UtcNow;
        Touch();
    }

    /// <summary>Enforces the one cross-row invariant CHECK cannot express: at least one
    /// active (non-lost), required (non-optional) line must exist. The total is derived
    /// from those lines here — optional lines are unselected alternatives and do not count —
    /// and this is the single declared rounding point (17 §3.4).</summary>
    public void Win()
    {
        if (Status != OpportunityStatus.Open)
            throw new InvalidOperationException($"Cannot win an opportunity in status {Status}. It must be open first.");

        var billableLines = _lines.Where(line => !line.IsOptional && !line.IsCanceled).ToList();
        if (billableLines.Count == 0)
            throw new InvalidOperationException("Cannot win an opportunity without at least one active required line.");

        // line_total is NULL on rows persisted before it was computed; derive it rather than count it as zero.
        var computedTotal = billableLines.Sum(line => line.LineTotal ?? line.Quantity * line.UnitPrice);

        Status = OpportunityStatus.Won;
        // Single declared rounding point (17 §3.4): 4dp computed total rounds to the
        // currency's 2dp minor unit exactly once, here.
        TotalAmount = decimal.Round(computedTotal, 2, MidpointRounding.AwayFromZero);
        WonDate = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Lose(string lostReason)
    {
        if (Status is OpportunityStatus.Won or OpportunityStatus.Lost)
            throw new InvalidOperationException($"Cannot lose an opportunity in status {Status}.");
        if (string.IsNullOrWhiteSpace(lostReason))
            throw new ArgumentException("Lost reason is required.", nameof(lostReason));

        Status = OpportunityStatus.Lost;
        LostReason = lostReason;
        LostDate = DateTimeOffset.UtcNow;
        Touch();
    }

    public void CancelLine(OpportunityLine line, string cancelReason)
    {
        if (!_lines.Contains(line))
            throw new InvalidOperationException("Line does not belong to this opportunity.");
        if (Status is OpportunityStatus.Won or OpportunityStatus.Lost)
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
