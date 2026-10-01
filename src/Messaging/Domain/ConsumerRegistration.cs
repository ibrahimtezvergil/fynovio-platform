using Contracts;

namespace Messaging.Domain;

/// <summary>When a consumer was first seen by the relay. A <see cref="ConsumerStartPolicy.FromNow"/> consumer is owed
/// only facts that occurred after <see cref="RegisteredAt"/>; older ones are fanned out as skipped (E-3.8). Not
/// tenant data: one row per consumer, readable by the relay role only.</summary>
public sealed class ConsumerRegistration
{
    public string Consumer { get; private set; } = null!;
    public ConsumerStartPolicy StartPolicy { get; private set; }
    public DateTimeOffset RegisteredAt { get; private set; }

    private ConsumerRegistration() { }
}
