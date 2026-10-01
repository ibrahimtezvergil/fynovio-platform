using Contracts;

namespace CRM.Activity;

/// <summary>CRM's inbox (adr-event-consumption.md, E-3.3): one row per event a CRM consumer has applied, written in the
/// same transaction as the effect. The primary key <c>(consumer, event_id)</c> is what makes a redelivery a no-op.</summary>
public sealed class ConsumedEvent
{
    public string Consumer { get; private set; } = null!;
    public Guid EventId { get; private set; }
    public TenantId TenantId { get; private set; }
    public DateTimeOffset ConsumedAt { get; private set; }

    private ConsumedEvent() { }

    public static ConsumedEvent Record(string consumer, EventEnvelope envelope) => new()
    {
        Consumer = consumer,
        EventId = envelope.EventId,
        TenantId = envelope.TenantId,
        ConsumedAt = DateTimeOffset.UtcNow
    };
}
