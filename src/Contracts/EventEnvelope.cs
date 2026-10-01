namespace Contracts;

/// <summary>One published fact as a consumer sees it — the outbox row's CloudEvents-shaped columns (doc 04 §4), never
/// the producing module's outbox entity. <see cref="PayloadJson"/> is the producer's versioned payload, unchanged.</summary>
public sealed record EventEnvelope(
    Guid EventId,
    string EventType,
    string Source,
    string Subject,
    TenantId TenantId,
    string AggregateType,
    long AggregateId,
    long AggregateVersion,
    Guid CorrelationId,
    Guid? CausationId,
    DateTimeOffset OccurredAt,
    string PayloadJson);
