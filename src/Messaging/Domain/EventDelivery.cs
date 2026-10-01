using Contracts;

namespace Messaging.Domain;

/// <summary>One fact owed to one consumer (adr-event-consumption.md, E-3.3). A pointer, never a copy: the payload stays
/// in the producing module's outbox and is read under the tenant's context at delivery time. Written and advanced by
/// the relay role through raw SQL (<see cref="Relay.OutboxRelay"/>, <see cref="Delivery.DeliveryProcessor"/>); this
/// entity exists for the schema and for reads.</summary>
public sealed class EventDelivery
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Consumer { get; private set; } = null!;
    public string SourceSchema { get; private set; } = null!;
    public long SourceId { get; private set; }
    public Guid EventId { get; private set; }
    public string EventType { get; private set; } = null!;
    public string AggregateType { get; private set; } = null!;
    public long AggregateId { get; private set; }
    public long AggregateVersion { get; private set; }
    public DeliveryStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }

    private EventDelivery() { }
}

public enum DeliveryStatus
{
    Pending,
    Processing,
    Delivered,
    Skipped,
    Dead
}
