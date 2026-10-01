using Contracts;

namespace CRM.Activity;

/// <summary>One published opportunity fact, projected for the detail page's timeline (adr-event-consumption.md, E-2
/// (a)). A read model, never a source of truth: written only by <see cref="OpportunityActivityConsumer"/>, rebuildable
/// from the outbox, and authorized on read exactly like the opportunity itself. <see cref="Payload"/> is the producer's
/// own fact, kept so the read side can label it (stage names, owner names) at the reader's time.</summary>
public sealed class OpportunityActivityEntry
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public long OpportunityId { get; private set; }
    public Guid EventId { get; private set; }
    public string Kind { get; private set; } = null!;
    public long AggregateVersion { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string Payload { get; private set; } = null!;

    private OpportunityActivityEntry() { }

    public static OpportunityActivityEntry From(EventEnvelope envelope, string kind) => new()
    {
        TenantId = envelope.TenantId,
        OpportunityId = envelope.AggregateId,
        EventId = envelope.EventId,
        Kind = kind,
        AggregateVersion = envelope.AggregateVersion,
        OccurredAt = envelope.OccurredAt,
        Payload = envelope.PayloadJson
    };
}
