using Contracts;

namespace CRM.Evidence;

/// <summary>Append-only evidence-intent capture — doc 14 decision #4 is "state + outbox +
/// evidence commit together", three things, not two (schema revision 3, item 10). Written
/// in the same transaction as the domain state and outbox rows; a future Evidence module
/// reads or projects from this table once it exists. No update or delete path.</summary>
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
