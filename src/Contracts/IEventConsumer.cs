namespace Contracts;

/// <summary>A code-registered subscriber to published facts (adr-event-consumption.md, E-3). Delivery is at least once:
/// an implementation records <see cref="EventEnvelope.EventId"/> in its own module's inbox in the same transaction as
/// its effect, so a redelivery is acknowledged without re-applying. In this phase a consumer writes only its own
/// module's projections — never an aggregate, never a PDP-gated command (E-5).</summary>
public interface IEventConsumer
{
    /// <summary>Stable, unique name; the delivery ledger and the inbox are keyed on it. Never rename a live consumer.</summary>
    string Name { get; }

    IReadOnlySet<string> EventTypes { get; }

    /// <summary>What happens to facts that already existed when the consumer was first registered.</summary>
    ConsumerStartPolicy StartPolicy { get; }

    /// <summary>Runs with no ambient tenant: the implementation sets its own tenant context from
    /// <see cref="EventEnvelope.TenantId"/>. Throwing schedules a retry.</summary>
    Task HandleAsync(EventEnvelope envelope, CancellationToken cancellationToken);
}

public enum ConsumerStartPolicy
{
    /// <summary>Every fact, including those published before the consumer existed — for replay-safe projections.</summary>
    FromBeginning,

    /// <summary>Only facts published after the consumer was registered; older ones are recorded as skipped — for
    /// consumers with external side effects.</summary>
    FromNow
}
