using Contracts;

namespace Access.Evidence;

/// <summary>Append-only. `Detail` carries the JSON of what changed — Access config
/// mutations count as risk-catalogued per AGENTS.md, so every Grant/Revoke writes one.
/// Deliberately duplicated from `CRM.Evidence.EvidenceRecord`.</summary>
public sealed class EvidenceRecord
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string AggregateType { get; private set; } = null!;
    public long AggregateId { get; private set; }
    public long AggregateVersion { get; private set; }
    public string PrincipalIssuer { get; private set; } = null!;
    public string PrincipalSubject { get; private set; } = null!;
    public string Action { get; private set; } = null!;
    public string Detail { get; private set; } = null!;
    public Guid CorrelationId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private EvidenceRecord() { }

    public static EvidenceRecord Create(
        TenantId tenantId,
        string aggregateType,
        long aggregateId,
        long aggregateVersion,
        PrincipalRef principal,
        string action,
        string detail,
        Guid correlationId)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action is required.", nameof(action));
        if (string.IsNullOrWhiteSpace(detail))
            throw new ArgumentException("Detail is required.", nameof(detail));

        return new EvidenceRecord
        {
            TenantId = tenantId,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            AggregateVersion = aggregateVersion,
            PrincipalIssuer = principal.Issuer,
            PrincipalSubject = principal.Subject,
            Action = action,
            Detail = detail,
            CorrelationId = correlationId,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }
}
