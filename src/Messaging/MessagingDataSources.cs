using Npgsql;

namespace Messaging;

/// <summary>Connections as <c>fynovio_relay</c>: outbox pointer columns and the ledger, across tenants.</summary>
public sealed class RelayDataSource(NpgsqlDataSource source)
{
    public NpgsqlDataSource Source { get; } = source;
}

/// <summary>Connections as the runtime role: one event's full outbox row, under that event's tenant.</summary>
public sealed class RuntimeDataSource(NpgsqlDataSource source)
{
    public NpgsqlDataSource Source { get; } = source;
}
