using Contracts;
using Npgsql;

namespace Messaging.Tests;

[Collection(MessagingCollection.Name)]
public sealed class OutboxRelayTests(MessagingFixture fixture)
{
    private async Task<long> DeliveriesAsync(string consumer, Guid eventId, string? status = null) =>
        await fixture.AdminScalarAsync<long>(
            "SELECT count(*) FROM messaging.event_deliveries WHERE consumer = @consumer AND event_id = @event AND (@status = '' OR status = @status)",
            ("consumer", consumer), ("event", eventId), ("status", status ?? ""));

    private Task<bool> ProcessedAsync(string schema, long id) =>
        fixture.AdminScalarAsync<bool>($"SELECT processed_at IS NOT NULL FROM {schema}.outbox_messages WHERE id = @id", ("id", id));

    [Fact]
    public async Task Fans_out_every_schema_and_tenant_to_the_subscribed_consumer_and_marks_the_rows_processed()
    {
        var eventType = $"test.fanout.{Guid.NewGuid():N}.v1";
        await using var harness = new MessagingHarness(fixture, new HashSet<string> { eventType });
        var seeded = new List<(string Schema, long Id, Guid EventId)>();
        foreach (var schema in OutboxSources.All)
        {
            foreach (var tenant in new[] { TestTenants.Next(), TestTenants.Next() })
            {
                var (id, eventId) = await fixture.InsertOutboxAsync(schema, tenant, eventType);
                seeded.Add((schema, id, eventId));
            }
        }
        var (otherId, otherEventId) = await fixture.InsertOutboxAsync("crm", TestTenants.Next(), "test.nobody-listens.v1");

        await harness.Relay.RelayOnceAsync(CancellationToken.None);

        foreach (var (schema, id, eventId) in seeded)
        {
            Assert.Equal(1, await DeliveriesAsync(harness.ConsumerName, eventId, "pending"));
            Assert.True(await ProcessedAsync(schema, id));
        }
        Assert.Equal(0, await DeliveriesAsync(harness.ConsumerName, otherEventId));
        Assert.True(await ProcessedAsync("crm", otherId));
    }

    [Fact]
    public async Task Relaying_a_row_again_creates_no_second_delivery()
    {
        var eventType = $"test.again.{Guid.NewGuid():N}.v1";
        await using var harness = new MessagingHarness(fixture, new HashSet<string> { eventType });
        var (id, eventId) = await fixture.InsertOutboxAsync("masterdata", TestTenants.Next(), eventType);

        await harness.Relay.RelayOnceAsync(CancellationToken.None);
        // A crash after the ledger insert but before the outbox mark looks like this on restart.
        await fixture.AdminExecuteAsync("UPDATE masterdata.outbox_messages SET processed_at = NULL WHERE id = @id", ("id", id));
        await harness.Relay.RelayOnceAsync(CancellationToken.None);

        Assert.Equal(1, await DeliveriesAsync(harness.ConsumerName, eventId));
        Assert.True(await ProcessedAsync("masterdata", id));
    }

    [Fact]
    public async Task A_from_now_consumer_is_owed_only_facts_after_its_registration()
    {
        var eventType = $"test.fromnow.{Guid.NewGuid():N}.v1";
        await using var harness = new MessagingHarness(fixture, new HashSet<string> { eventType }, ConsumerStartPolicy.FromNow);
        await harness.Relay.RelayOnceAsync(CancellationToken.None); // registers the consumer

        var tenant = TestTenants.Next();
        var (_, before) = await fixture.InsertOutboxAsync("access", tenant, eventType, occurredAt: DateTimeOffset.UtcNow.AddHours(-1));
        var (_, after) = await fixture.InsertOutboxAsync("access", tenant, eventType, occurredAt: DateTimeOffset.UtcNow.AddMinutes(1));
        await harness.Relay.RelayOnceAsync(CancellationToken.None);

        Assert.Equal(1, await DeliveriesAsync(harness.ConsumerName, before, "skipped"));
        Assert.Equal(1, await DeliveriesAsync(harness.ConsumerName, after, "pending"));
    }

    [Fact]
    public async Task A_schema_whose_relay_lock_is_held_elsewhere_is_left_for_the_next_pass()
    {
        var eventType = $"test.locked.{Guid.NewGuid():N}.v1";
        await using var harness = new MessagingHarness(fixture, new HashSet<string> { eventType });
        var tenant = TestTenants.Next();
        var (lockedId, lockedEvent) = await fixture.InsertOutboxAsync("collaboration", tenant, eventType);
        var (freeId, _) = await fixture.InsertOutboxAsync("tenant_lifecycle", tenant, eventType);

        await using (var other = new NpgsqlConnection(fixture.AdminConnectionString))
        {
            await other.OpenAsync();
            await using var transaction = await other.BeginTransactionAsync();
            await using (var take = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext('fynovio.messaging.relay.collaboration'))", other, transaction))
                await take.ExecuteNonQueryAsync();

            await harness.Relay.RelayOnceAsync(CancellationToken.None);

            Assert.False(await ProcessedAsync("collaboration", lockedId));
            Assert.Equal(0, await DeliveriesAsync(harness.ConsumerName, lockedEvent));
            Assert.True(await ProcessedAsync("tenant_lifecycle", freeId));
        }

        await harness.Relay.RelayOnceAsync(CancellationToken.None);
        Assert.True(await ProcessedAsync("collaboration", lockedId));
        Assert.Equal(1, await DeliveriesAsync(harness.ConsumerName, lockedEvent));
    }

    [Fact]
    public async Task A_from_beginning_consumer_added_later_gets_the_history_already_fanned_out()
    {
        var eventType = $"test.history.{Guid.NewGuid():N}.v1";
        var tenant = TestTenants.Next();
        var (oldId, oldEvent) = await fixture.InsertOutboxAsync("collaboration", tenant, eventType);
        await using (var early = new MessagingHarness(fixture, new HashSet<string> { "test.unrelated.v1" }))
            await early.Relay.RelayOnceAsync(CancellationToken.None);
        Assert.True(await ProcessedAsync("collaboration", oldId));

        await using var late = new MessagingHarness(fixture, new HashSet<string> { eventType });
        var (_, newEvent) = await fixture.InsertOutboxAsync("collaboration", tenant, eventType);
        await late.Relay.RelayOnceAsync(CancellationToken.None);
        await late.Relay.RelayOnceAsync(CancellationToken.None);

        Assert.Equal(1, await DeliveriesAsync(late.ConsumerName, oldEvent, "pending"));
        Assert.Equal(1, await DeliveriesAsync(late.ConsumerName, newEvent, "pending"));
    }

    [Fact]
    public async Task A_from_now_consumer_added_later_gets_no_history()
    {
        var eventType = $"test.nohistory.{Guid.NewGuid():N}.v1";
        var (oldId, oldEvent) = await fixture.InsertOutboxAsync("crm", TestTenants.Next(), eventType);
        await using (var early = new MessagingHarness(fixture, new HashSet<string> { "test.unrelated.v1" }))
            await early.Relay.RelayOnceAsync(CancellationToken.None);
        Assert.True(await ProcessedAsync("crm", oldId));

        await using var late = new MessagingHarness(fixture, new HashSet<string> { eventType }, ConsumerStartPolicy.FromNow);
        await late.Relay.RelayOnceAsync(CancellationToken.None);

        Assert.Equal(0, await DeliveriesAsync(late.ConsumerName, oldEvent));
    }
}
