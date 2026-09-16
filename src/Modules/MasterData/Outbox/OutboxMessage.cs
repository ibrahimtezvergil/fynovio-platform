using Contracts;

namespace MasterData.Outbox;

/// <summary>Written in the same SaveChanges() transaction as the domain state it
/// describes (doc 14 decision #4). Envelope matches doc 04 §4's CloudEvents wire
/// profile — see docs/schema/crm-sales-schema.md revision 2, item 5.</summary>
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
    public Guid? CausationId { get; private set; }
    public string Payload { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(
        TenantId tenantId,
        string aggregateType,
        long aggregateId,
        long aggregateVersion,
        string eventType,
        string source,
        string subject,
        Guid correlationId,
        Guid? causationId,
        string payload)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("Event type is required.", nameof(eventType));
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Payload is required.", nameof(payload));

        return new OutboxMessage
        {
            TenantId = tenantId,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            AggregateVersion = aggregateVersion,
            EventId = Guid.NewGuid(),
            EventType = eventType,
            Source = source,
            Subject = subject,
            CorrelationId = correlationId,
            CausationId = causationId,
            Payload = payload,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    public void MarkProcessed()
    {
        if (ProcessedAt is not null)
            throw new InvalidOperationException("Outbox message already processed.");

        ProcessedAt = DateTimeOffset.UtcNow;
    }
}
