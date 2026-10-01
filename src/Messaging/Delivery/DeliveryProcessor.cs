using Contracts;
using Messaging.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Messaging.Delivery;

/// <summary>Claims due deliveries and hands each to its consumer (E-3.5–E-3.7).
///
/// A claim is a lease: the row turns <c>processing</c> with <c>locked_until</c>, and no transaction is held while the
/// consumer runs; an expired lease is claimable again (implementation note 5). A delivery is not claimable while an
/// earlier version of the same aggregate is still owed to the same consumer — including a dead one, which blocks only
/// that aggregate. Only deliveries for consumers registered in this process are claimed, so a worker running an older
/// build never fails another build's consumer's work. Failures back off exponentially and end <c>dead</c> after <see cref="MaxAttempts"/>.</summary>
public sealed class DeliveryProcessor(
    RelayDataSource relay,
    ConsumerCatalog catalog,
    OutboxEnvelopeReader envelopes,
    IServiceScopeFactory scopeFactory,
    ILogger<DeliveryProcessor> logger)
{
    public const int BatchSize = 50;
    public const int MaxAttempts = 10;
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ConsumerTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan BackoffBase = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan BackoffCap = TimeSpan.FromMinutes(15);

    /// <summary>The wait before attempt <paramref name="attempts"/> + 1: 5 s × 2^(attempts − 1), at most 15 min.</summary>
    public static TimeSpan Backoff(int attempts)
    {
        var seconds = BackoffBase.TotalSeconds * Math.Pow(2, Math.Max(0, attempts - 1));
        return seconds >= BackoffCap.TotalSeconds ? BackoffCap : TimeSpan.FromSeconds(seconds);
    }

    /// <summary>Returns the number of deliveries claimed in this pass.</summary>
    public async Task<int> ProcessOnceAsync(CancellationToken cancellationToken)
    {
        if (catalog.Consumers.Count == 0)
            return 0;

        var claimed = await ClaimAsync(cancellationToken);
        foreach (var delivery in claimed)
            await DeliverAsync(delivery, cancellationToken);
        return claimed.Count;
    }

    private async Task<List<ClaimedDelivery>> ClaimAsync(CancellationToken cancellationToken)
    {
        await using var connection = await relay.Source.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            $"""
            UPDATE messaging.event_deliveries d
            SET status = '{DeliveryStatusText.Processing}', locked_until = now() + @lease, attempts = d.attempts + 1
            WHERE d.id IN (
                SELECT c.id FROM messaging.event_deliveries c
                WHERE c.consumer = ANY(@consumers)
                  AND ((c.status = '{DeliveryStatusText.Pending}' AND c.next_attempt_at <= now())
                       OR (c.status = '{DeliveryStatusText.Processing}' AND c.locked_until < now()))
                  AND NOT EXISTS (
                      SELECT 1 FROM messaging.event_deliveries e
                      WHERE e.consumer = c.consumer AND e.tenant_id = c.tenant_id AND e.source_schema = c.source_schema
                        AND e.aggregate_type = c.aggregate_type AND e.aggregate_id = c.aggregate_id
                        AND e.status NOT IN ('{DeliveryStatusText.Delivered}', '{DeliveryStatusText.Skipped}')
                        AND (e.aggregate_version, e.source_id) < (c.aggregate_version, c.source_id))
                ORDER BY c.id
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED)
            RETURNING d.id, d.tenant_id, d.consumer, d.source_schema, d.source_id, d.attempts
            """,
            connection);
        command.Parameters.AddWithValue("lease", Lease);
        command.Parameters.AddWithValue("consumers", catalog.Consumers.Select(c => c.Name).ToArray());

        var claimed = new List<ClaimedDelivery>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            claimed.Add(new ClaimedDelivery(reader.GetInt64(0), new TenantId(reader.GetInt64(1)), reader.GetString(2),
                reader.GetString(3), reader.GetInt64(4), reader.GetInt32(5)));
        }
        return claimed.OrderBy(d => d.Id).ToList();
    }

    private async Task DeliverAsync(ClaimedDelivery delivery, CancellationToken cancellationToken)
    {
        try
        {
            var envelope = await envelopes.ReadAsync(delivery.SourceSchema, delivery.SourceId, delivery.TenantId, cancellationToken)
                ?? throw new SourceEventMissingException();

            using var scope = scopeFactory.CreateScope();
            var consumer = scope.ServiceProvider.GetServices<IEventConsumer>().SingleOrDefault(c => c.Name == delivery.Consumer)
                ?? throw new ConsumerNotRegisteredException();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ConsumerTimeout);
            await consumer.HandleAsync(envelope, timeout.Token);

            await CompleteAsync(delivery, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down: the lease expires and the delivery is claimed again.
            throw;
        }
        catch (Exception exception)
        {
            await FailAsync(delivery, exception, cancellationToken);
        }
    }

    private async Task CompleteAsync(ClaimedDelivery delivery, CancellationToken cancellationToken)
    {
        await using var connection = await relay.Source.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            $"""
            UPDATE messaging.event_deliveries
            SET status = '{DeliveryStatusText.Delivered}', delivered_at = now(), locked_until = NULL, last_error = NULL
            WHERE id = @id AND status = '{DeliveryStatusText.Processing}' AND attempts = @attempts
            """,
            connection);
        command.Parameters.AddWithValue("id", delivery.Id);
        command.Parameters.AddWithValue("attempts", delivery.Attempts);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task FailAsync(ClaimedDelivery delivery, Exception exception, CancellationToken cancellationToken)
    {
        var dead = delivery.Attempts >= MaxAttempts;
        var error = Describe(exception);
        logger.LogWarning("Delivery {DeliveryId} to {Consumer} failed (attempt {Attempt}): {Error}", delivery.Id, delivery.Consumer, delivery.Attempts, error);

        await using var connection = await relay.Source.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            $"""
            UPDATE messaging.event_deliveries
            SET status = @status, next_attempt_at = now() + @backoff, locked_until = NULL, last_error = @error
            WHERE id = @id AND status = '{DeliveryStatusText.Processing}' AND attempts = @attempts
            """,
            connection);
        command.Parameters.AddWithValue("status", dead ? DeliveryStatusText.Dead : DeliveryStatusText.Pending);
        command.Parameters.AddWithValue("backoff", Backoff(delivery.Attempts));
        command.Parameters.AddWithValue("error", error);
        command.Parameters.AddWithValue("id", delivery.Id);
        command.Parameters.AddWithValue("attempts", delivery.Attempts);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>The exception type, plus the SQLSTATE for a database error — never a message or detail, which can carry
    /// key values (implementation note 6).</summary>
    public static string Describe(Exception exception) => exception switch
    {
        PostgresException postgres => $"{nameof(PostgresException)} {postgres.SqlState}",
        _ => exception.GetType().Name
    };

    private sealed record ClaimedDelivery(long Id, TenantId TenantId, string Consumer, string SourceSchema, long SourceId, int Attempts);
}

/// <summary>The outbox row a delivery points at is not visible under the delivery's tenant.</summary>
public sealed class SourceEventMissingException() : Exception("The source event is not visible under the delivery's tenant.");

/// <summary>A delivery names a consumer that is no longer registered.</summary>
public sealed class ConsumerNotRegisteredException() : Exception("The delivery's consumer is not registered.");
