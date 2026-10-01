using Contracts;
using Messaging.Persistence;
using Npgsql;
using NpgsqlTypes;

namespace Messaging.Relay;

/// <summary>Fans pending outbox rows out into the delivery ledger (E-3.3): one delivery per subscribed consumer, then
/// <c>processed_at</c> on the outbox row — "fanned out", no longer "logged". Runs as the relay role, so it reads only
/// the pointer columns and never a payload (E-1 (a)).
///
/// One writer per schema: a transaction-scoped advisory lock keeps two relays from committing a later row of an
/// aggregate before an earlier one reaches the ledger (implementation note 4). A pass that cannot take the lock skips
/// that schema until the next poll.</summary>
public sealed class OutboxRelay(RelayDataSource relay, ConsumerCatalog catalog)
{
    public const int BatchSize = 100;

    /// <summary>Returns the number of outbox rows marked processed in this pass.</summary>
    public async Task<int> RelayOnceAsync(CancellationToken cancellationToken)
    {
        await using var connection = await relay.Source.OpenConnectionAsync(cancellationToken);
        var registeredAt = await RegisterConsumersAsync(connection, cancellationToken);

        var processed = 0;
        foreach (var schema in OutboxSources.All)
            processed += await RelaySchemaAsync(connection, schema, registeredAt, cancellationToken);
        return processed;
    }

    /// <summary>Records each consumer the first time the relay sees it. A new <see cref="ConsumerStartPolicy.FromBeginning"/>
    /// consumer is owed the facts already fanned out before it existed, so its history is backfilled in the same
    /// transaction as its registration — a crash between the two can never leave it registered but without its past.</summary>
    private async Task<Dictionary<string, DateTimeOffset>> RegisterConsumersAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        foreach (var consumer in catalog.Consumers)
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using (var insert = new NpgsqlCommand(
                "INSERT INTO messaging.consumer_registrations (consumer, start_policy) VALUES (@consumer, @policy) ON CONFLICT (consumer) DO NOTHING RETURNING consumer",
                connection, transaction))
            {
                insert.Parameters.AddWithValue("consumer", consumer.Name);
                insert.Parameters.AddWithValue("policy", consumer.StartPolicy.ToString());
                var isNew = await insert.ExecuteScalarAsync(cancellationToken) is not null;
                if (isNew && consumer.StartPolicy == ConsumerStartPolicy.FromBeginning)
                {
                    foreach (var schema in OutboxSources.All)
                        await BackfillAsync(connection, transaction, schema, consumer, cancellationToken);
                }
            }
            await transaction.CommitAsync(cancellationToken);
        }

        var registeredAt = new Dictionary<string, DateTimeOffset>();
        await using var select = new NpgsqlCommand("SELECT consumer, registered_at FROM messaging.consumer_registrations", connection);
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            registeredAt[reader.GetString(0)] = reader.GetFieldValue<DateTimeOffset>(1);
        return registeredAt;
    }

    private async Task<int> RelaySchemaAsync(NpgsqlConnection connection, string schema, IReadOnlyDictionary<string, DateTimeOffset> registeredAt, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var lockCommand = new NpgsqlCommand("SELECT pg_try_advisory_xact_lock(hashtext(@key))", connection, transaction))
        {
            lockCommand.Parameters.AddWithValue("key", $"fynovio.messaging.relay.{schema}");
            if (await lockCommand.ExecuteScalarAsync(cancellationToken) is not true)
                return 0;
        }

        var pointers = await ReadPendingAsync(connection, transaction, schema, cancellationToken);
        if (pointers.Count == 0)
            return 0;

        foreach (var pointer in pointers)
        {
            foreach (var consumer in catalog.Consumers.Where(c => c.EventTypes.Contains(pointer.EventType)))
            {
                var skipped = consumer.StartPolicy == ConsumerStartPolicy.FromNow && pointer.OccurredAt < registeredAt[consumer.Name];
                await InsertDeliveryAsync(connection, transaction, schema, pointer, consumer.Name, skipped, cancellationToken);
            }
        }

        await using (var mark = new NpgsqlCommand($"UPDATE {OutboxSources.Require(schema)}.outbox_messages SET processed_at = now() WHERE id = ANY(@ids)", connection, transaction))
        {
            mark.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = pointers.Select(p => p.Id).ToArray() });
            await mark.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return pointers.Count;
    }

    /// <summary>Rows already marked processed (fanned out to the consumers that existed then) become deliveries of a
    /// newly registered consumer. Rows still pending are left to the normal fan-out, which now includes it.</summary>
    private static async Task BackfillAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string schema, ConsumerDescriptor consumer, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            $"""
            INSERT INTO messaging.event_deliveries
                (tenant_id, consumer, source_schema, source_id, event_id, event_type, aggregate_type, aggregate_id, aggregate_version, status, attempts, next_attempt_at)
            SELECT tenant_id, @consumer, @schema, id, event_id, event_type, aggregate_type, aggregate_id, aggregate_version, '{DeliveryStatusText.Pending}', 0, now()
            FROM {OutboxSources.Require(schema)}.outbox_messages
            WHERE processed_at IS NOT NULL AND event_type = ANY(@types)
            ON CONFLICT (consumer, event_id) DO NOTHING
            """,
            connection, transaction);
        command.Parameters.AddWithValue("consumer", consumer.Name);
        command.Parameters.AddWithValue("schema", schema);
        command.Parameters.AddWithValue("types", consumer.EventTypes.ToArray());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<List<OutboxPointer>> ReadPendingAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string schema, CancellationToken cancellationToken)
    {
        // Pointer columns only — the relay role has no SELECT on anything else (scripts/create-relay-role.sql).
        await using var command = new NpgsqlCommand(
            $"""
            SELECT id, tenant_id, event_id, event_type, aggregate_type, aggregate_id, aggregate_version, occurred_at
            FROM {OutboxSources.Require(schema)}.outbox_messages
            WHERE processed_at IS NULL
            ORDER BY id
            LIMIT {BatchSize}
            """,
            connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var pointers = new List<OutboxPointer>();
        while (await reader.ReadAsync(cancellationToken))
        {
            pointers.Add(new OutboxPointer(
                reader.GetInt64(0), reader.GetInt64(1), reader.GetGuid(2), reader.GetString(3),
                reader.GetString(4), reader.GetInt64(5), reader.GetInt64(6), reader.GetFieldValue<DateTimeOffset>(7)));
        }
        return pointers;
    }

    private static async Task InsertDeliveryAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string schema, OutboxPointer pointer, string consumer, bool skipped, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO messaging.event_deliveries
                (tenant_id, consumer, source_schema, source_id, event_id, event_type, aggregate_type, aggregate_id, aggregate_version, status, attempts, next_attempt_at)
            VALUES (@tenant, @consumer, @schema, @source, @event, @type, @aggregateType, @aggregateId, @version, @status, 0, now())
            ON CONFLICT (consumer, event_id) DO NOTHING
            """,
            connection, transaction);
        command.Parameters.AddWithValue("tenant", pointer.TenantId);
        command.Parameters.AddWithValue("consumer", consumer);
        command.Parameters.AddWithValue("schema", schema);
        command.Parameters.AddWithValue("source", pointer.Id);
        command.Parameters.AddWithValue("event", pointer.EventId);
        command.Parameters.AddWithValue("type", pointer.EventType);
        command.Parameters.AddWithValue("aggregateType", pointer.AggregateType);
        command.Parameters.AddWithValue("aggregateId", pointer.AggregateId);
        command.Parameters.AddWithValue("version", pointer.AggregateVersion);
        command.Parameters.AddWithValue("status", skipped ? DeliveryStatusText.Skipped : DeliveryStatusText.Pending);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record OutboxPointer(long Id, long TenantId, Guid EventId, string EventType, string AggregateType, long AggregateId, long AggregateVersion, DateTimeOffset OccurredAt);
}
