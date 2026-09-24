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
    public long? OpportunityTypeId { get; private set; }
    public long? PipelineDefinitionVersionId { get; private set; }
    public long? PipelineStageId { get; private set; }
    public string AssignedPrincipalIssuer { get; private set; } = null!;
    public string AssignedPrincipalSubject { get; private set; } = null!;
    public OpportunityStatus Status { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
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
        decimal estimatedAmount,
        long? opportunityTypeId = null)
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
            OpportunityTypeId = opportunityTypeId,
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
    /// mandatory from this point on, diverging from legacy's skip-behavior on purpose.
    /// Assigns the entry pipeline stage if the caller resolved one (architecture plan
    /// §2.3) — the domain layer doesn't query the database, so OpenOpportunityHandler
    /// resolves which version/stage applies and passes them in; a tenant with no
    /// pipeline configured yet passes both as null and Open proceeds normally.</summary>
    public void Open(DateTimeOffset expiryDate, long? pipelineDefinitionVersionId, long? pipelineStageId)
    {
        EnsureNotArchived();
        if (Status != OpportunityStatus.Draft)
            throw new InvalidOperationException($"Cannot open an opportunity in status {Status}.");
        if (expiryDate <= DateTimeOffset.UtcNow)
            throw new ArgumentOutOfRangeException(nameof(expiryDate), "Expiry date must be in the future.");
        if (pipelineStageId is not null && pipelineDefinitionVersionId is null)
            throw new ArgumentException("A pipeline stage requires its pipeline definition version.", nameof(pipelineStageId));

        Status = OpportunityStatus.Open;
        ExpiryDate = expiryDate;
        OpenedDate = DateTimeOffset.UtcNow;
        PipelineDefinitionVersionId = pipelineDefinitionVersionId;
        PipelineStageId = pipelineStageId;
        Touch();
    }

    /// <summary>Enforces the one cross-row invariant CHECK cannot express: at least one
    /// active (non-lost), required (non-optional) line must exist. The total is derived
    /// from those lines here — optional lines are unselected alternatives and do not count —
    /// and this is the single declared rounding point (17 §3.4).</summary>
    public void Win(bool requireActiveRequiredLine = true)
    {
        EnsureNotArchived();
        if (Status != OpportunityStatus.Open)
            throw new InvalidOperationException($"Cannot win an opportunity in status {Status}. It must be open first.");

        var billableLines = _lines.Where(line => !line.IsOptional && !line.IsCanceled).ToList();
        if (requireActiveRequiredLine && billableLines.Count == 0)
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
        EnsureNotArchived();
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

    /// <summary>Owner = "the principal currently responsible for this record" (round-3
    /// closure matrix "CRM Owner semantics"), reusing AssignedPrincipal as the single
    /// mutable owner field (architecture plan §2.2, option (a) — no new column). Blocked
    /// once terminal, same reasoning as CancelLine: a closed opportunity's history
    /// should not keep changing who "owns" it.</summary>
    public void Reassign(PrincipalRef newAssignedPrincipal)
    {
        if (Status is OpportunityStatus.Won or OpportunityStatus.Lost)
            throw new InvalidOperationException($"Cannot reassign an opportunity in status {Status}.");

        AssignedPrincipalIssuer = newAssignedPrincipal.Issuer;
        AssignedPrincipalSubject = newAssignedPrincipal.Subject;
        Touch();
    }

    /// <summary>Only the Status==Open precondition is enforced here — whether
    /// pipelineStageId actually belongs to this opportunity's current
    /// PipelineDefinitionVersionId, and whether it's active, are cross-aggregate facts
    /// ChangePipelineStageHandler validates before calling this (architecture plan §7/
    /// §9: the domain layer doesn't query another aggregate's table). Pipeline stage
    /// changes never imply a canonical lifecycle transition (binding spec §9) — Won/Lost
    /// remain Win()/Lose()'s job alone.</summary>
    public void ChangeStage(long pipelineStageId)
    {
        EnsureNotArchived();
        if (Status != OpportunityStatus.Open)
            throw new InvalidOperationException($"Cannot change pipeline stage on an opportunity in status {Status}. It must be open.");

        PipelineStageId = pipelineStageId;
        Touch();
    }

    public void Archive(bool confirmOpenOpportunity)
    {
        if (Status is OpportunityStatus.Won or OpportunityStatus.Lost)
            throw new InvalidOperationException("Won and Lost opportunities cannot be archived.");
        if (IsArchived)
            return;
        if (Status == OpportunityStatus.Open && !confirmOpenOpportunity)
            throw new InvalidOperationException("Archiving an open opportunity requires explicit confirmation.");

        IsArchived = true;
        ArchivedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Restore(long? replacementActiveStageId = null)
    {
        if (!IsArchived)
            return;
        if (replacementActiveStageId is not null)
            PipelineStageId = replacementActiveStageId;
        IsArchived = false;
        ArchivedAt = null;
        Touch();
    }

    private void EnsureNotArchived()
    {
        if (IsArchived)
            throw new InvalidOperationException("Archived opportunities cannot re-enter the active sales lifecycle.");
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
