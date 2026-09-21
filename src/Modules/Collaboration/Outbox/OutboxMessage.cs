using Contracts;

namespace Collaboration.Outbox;

public sealed class OutboxMessage
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string AggregateType { get; private set; } = null!;
    public long AggregateId { get; private set; }
    public long AggregateVersion { get; private set; }
    public Guid EventId { get; private set; }
    public string EventType { get; private set; } = null!;
    public string Source { get; private set; } = null!;
    public string Subject { get; private set; } = null!;
    public Guid CorrelationId { get; private set; }
    public string Payload { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(TenantId tenantId, string aggregateType, long aggregateId, long version, string eventType, string source, string subject, Guid correlationId, string payload) =>
        new() { TenantId = tenantId, AggregateType = aggregateType, AggregateId = aggregateId, AggregateVersion = version, EventId = Guid.NewGuid(), EventType = eventType, Source = source, Subject = subject, CorrelationId = correlationId, Payload = payload, OccurredAt = DateTimeOffset.UtcNow };
}
