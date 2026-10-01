using Contracts;
using Npgsql;

namespace Messaging.Delivery;

/// <summary>Reads one outbox row in full as the runtime role, under the row's own tenant — the normal
/// <c>tenant_isolation</c> policy decides what is visible, so a delivery whose tenant does not match its row reads
/// nothing.</summary>
public sealed class OutboxEnvelopeReader(RuntimeDataSource runtime)
{
    public async Task<EventEnvelope?> ReadAsync(string schema, long sourceId, TenantId tenantId, CancellationToken cancellationToken)
    {
        await using var connection = await runtime.Source.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var tenant = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, true)", connection, transaction))
        {
            tenant.Parameters.AddWithValue("tenant", tenantId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await tenant.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = new NpgsqlCommand(
            $"""
            SELECT event_id, event_type, source, subject, tenant_id, aggregate_type, aggregate_id, aggregate_version,
                   correlation_id, causation_id, occurred_at, payload::text
            FROM {OutboxSources.Require(schema)}.outbox_messages
            WHERE id = @id
            """,
            connection, transaction);
        command.Parameters.AddWithValue("id", sourceId);

        EventEnvelope? envelope = null;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                envelope = new EventEnvelope(
                    reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    new TenantId(reader.GetInt64(4)), reader.GetString(5), reader.GetInt64(6), reader.GetInt64(7),
                    reader.GetGuid(8), reader.IsDBNull(9) ? null : reader.GetGuid(9),
                    reader.GetFieldValue<DateTimeOffset>(10), reader.GetString(11));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return envelope;
    }
}
