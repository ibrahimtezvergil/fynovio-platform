using System.Text.Json;
using Contracts;
using Messaging.Delivery;
using Npgsql;

namespace Messaging.Tests;

[Collection(MessagingCollection.Name)]
public sealed class DeliveryProcessorTests(MessagingFixture fixture)
{
    private Task<string?> StatusAsync(string consumer, Guid eventId) =>
        fixture.AdminScalarAsync<string>("SELECT status FROM messaging.event_deliveries WHERE consumer = @c AND event_id = @e", ("c", consumer), ("e", eventId));

    private Task<int> AttemptsAsync(string consumer, Guid eventId) =>
        fixture.AdminScalarAsync<int>("SELECT attempts FROM messaging.event_deliveries WHERE consumer = @c AND event_id = @e", ("c", consumer), ("e", eventId));

    private Task MakeDueAsync(string consumer) =>
        fixture.AdminExecuteAsync("UPDATE messaging.event_deliveries SET next_attempt_at = now() - interval '1 second' WHERE consumer = @c AND status = 'pending'", ("c", consumer));

    public static TheoryData<string> Schemas => new(OutboxSources.All);

    [Theory]
    [MemberData(nameof(Schemas))]
    public async Task Delivers_the_full_envelope_read_under_the_events_own_tenant(string schema)
    {
        var eventType = $"test.envelope.{Guid.NewGuid():N}.v1";
        await using var harness = new MessagingHarness(fixture, new HashSet<string> { eventType });
        var tenant = TestTenants.Next();
        var (id, eventId) = await fixture.InsertOutboxAsync(schema, tenant, eventType, aggregateId: 42, version: 7, aggregateType: "Widget", payload: """{"a":1,"b":"x"}""");

        await harness.Relay.RelayOnceAsync(CancellationToken.None);
        await harness.Processor.ProcessOnceAsync(CancellationToken.None);

        var envelope = Assert.Single(harness.Probe.Received);
        Assert.Equal(eventId, envelope.EventId);
        Assert.Equal(eventType, envelope.EventType);
        Assert.Equal(new TenantId(tenant), envelope.TenantId);
        Assert.Equal(("Widget", 42L, 7L), (envelope.AggregateType, envelope.AggregateId, envelope.AggregateVersion));
        Assert.Equal(("/test", "things/1"), (envelope.Source, envelope.Subject));
        Assert.NotNull(envelope.CausationId);
        using var payload = JsonDocument.Parse(envelope.PayloadJson);
        Assert.Equal(1, payload.RootElement.GetProperty("a").GetInt32());
        Assert.Equal("x", payload.RootElement.GetProperty("b").GetString());

        Assert.Equal("delivered", await StatusAsync(harness.ConsumerName, eventId));
        Assert.Equal(id, await fixture.AdminScalarAsync<long>("SELECT source_id FROM messaging.event_deliveries WHERE event_id = @e", ("e", eventId)));
    }

    [Fact]
    public async Task A_delivery_whose_tenant_does_not_own_the_row_never_reaches_the_consumer()
    {
        var eventType = $"test.crosstenant.{Guid.NewGuid():N}.v1";
        await using var harness = new MessagingHarness(fixture, new HashSet<string> { eventType });
        var (_, eventId) = await fixture.InsertOutboxAsync("crm", TestTenants.Next(), eventType);
        await harness.Relay.RelayOnceAsync(CancellationToken.None);
        await fixture.AdminExecuteAsync("UPDATE messaging.event_deliveries SET tenant_id = @other WHERE event_id = @e", ("other", TestTenants.Next()), ("e", eventId));

        await harness.Processor.ProcessOnceAsync(CancellationToken.None);

        Assert.Empty(harness.Probe.Received);
        Assert.Equal("pending", await StatusAsync(harness.ConsumerName, eventId));
        Assert.Equal(nameof(SourceEventMissingException),
            await fixture.AdminScalarAsync<string>("SELECT last_error FROM messaging.event_deliveries WHERE event_id = @e", ("e", eventId)));
    }

    [Fact]
    public async Task A_failure_backs_off_records_only_the_error_type_and_ends_dead_after_the_last_attempt()
    {
        var eventType = $"test.poison.{Guid.NewGuid():N}.v1";
        await using var harness = new MessagingHarness(fixture, new HashSet<string> { eventType });
        harness.Probe.Fail = e => new InvalidOperationException($"bad payload {e.PayloadJson}");
        var (_, eventId) = await fixture.InsertOutboxAsync("crm", TestTenants.Next(), eventType);
        await harness.Relay.RelayOnceAsync(CancellationToken.None);

        await harness.Processor.ProcessOnceAsync(CancellationToken.None);

        Assert.Equal("pending", await StatusAsync(harness.ConsumerName, eventId));
        Assert.Equal(1, await AttemptsAsync(harness.ConsumerName, eventId));
        Assert.Equal(nameof(InvalidOperationException),
            await fixture.AdminScalarAsync<string>("SELECT last_error FROM messaging.event_deliveries WHERE event_id = @e", ("e", eventId)));
        Assert.True(await fixture.AdminScalarAsync<bool>("SELECT next_attempt_at > now() FROM messaging.event_deliveries WHERE event_id = @e", ("e", eventId)));

        // Not due yet: a second pass leaves it alone.
        await harness.Processor.ProcessOnceAsync(CancellationToken.None);
        Assert.Equal(1, await AttemptsAsync(harness.ConsumerName, eventId));

        await fixture.AdminExecuteAsync("UPDATE messaging.event_deliveries SET attempts = @n - 1 WHERE event_id = @e", ("n", DeliveryProcessor.MaxAttempts), ("e", eventId));
        await MakeDueAsync(harness.ConsumerName);
        await harness.Processor.ProcessOnceAsync(CancellationToken.None);

        Assert.Equal("dead", await StatusAsync(harness.ConsumerName, eventId));
        Assert.Equal(DeliveryProcessor.MaxAttempts, await AttemptsAsync(harness.ConsumerName, eventId));
        Assert.Empty(harness.Probe.Received);
    }

    [Fact]
    public async Task An_earlier_version_still_owed_blocks_only_the_later_versions_of_the_same_aggregate()
    {
        var eventType = $"test.order.{Guid.NewGuid():N}.v1";
        await using var harness = new MessagingHarness(fixture, new HashSet<string> { eventType });
        var tenant = TestTenants.Next();
        var (_, v1) = await fixture.InsertOutboxAsync("crm", tenant, eventType, aggregateId: 1, version: 1);
        var (_, v2) = await fixture.InsertOutboxAsync("crm", tenant, eventType, aggregateId: 1, version: 2);
        var (_, other) = await fixture.InsertOutboxAsync("crm", tenant, eventType, aggregateId: 2, version: 1);
        var failFirst = true;
        harness.Probe.Fail = e => e.EventId == v1 && failFirst ? new InvalidOperationException() : null;
        await harness.Relay.RelayOnceAsync(CancellationToken.None);

        await harness.Processor.ProcessOnceAsync(CancellationToken.None);

        Assert.Equal("pending", await StatusAsync(harness.ConsumerName, v1));
        Assert.Equal(0, await AttemptsAsync(harness.ConsumerName, v2));
        Assert.Equal("delivered", await StatusAsync(harness.ConsumerName, other));

        failFirst = false;
        await MakeDueAsync(harness.ConsumerName);
        await harness.Processor.ProcessOnceAsync(CancellationToken.None);
        await harness.Processor.ProcessOnceAsync(CancellationToken.None);

        Assert.Equal([other, v1, v2], harness.Probe.Received.Select(e => e.EventId));
    }

    [Fact]
    public async Task A_dead_earlier_version_keeps_blocking_its_aggregate()
    {
        var eventType = $"test.deadblock.{Guid.NewGuid():N}.v1";
        await using var harness = new MessagingHarness(fixture, new HashSet<string> { eventType });
        var tenant = TestTenants.Next();
        var (_, v1) = await fixture.InsertOutboxAsync("crm", tenant, eventType, aggregateId: 1, version: 1);
        var (_, v2) = await fixture.InsertOutboxAsync("crm", tenant, eventType, aggregateId: 1, version: 2);
        await harness.Relay.RelayOnceAsync(CancellationToken.None);
        await fixture.AdminExecuteAsync("UPDATE messaging.event_deliveries SET status = 'dead' WHERE event_id = @e", ("e", v1));

        await harness.Processor.ProcessOnceAsync(CancellationToken.None);

        Assert.Empty(harness.Probe.Received);
        Assert.Equal("pending", await StatusAsync(harness.ConsumerName, v2));
    }

    [Fact]
    public async Task An_expired_lease_is_claimed_again()
    {
        var eventType = $"test.lease.{Guid.NewGuid():N}.v1";
        await using var harness = new MessagingHarness(fixture, new HashSet<string> { eventType });
        var (_, eventId) = await fixture.InsertOutboxAsync("crm", TestTenants.Next(), eventType);
        await harness.Relay.RelayOnceAsync(CancellationToken.None);
        await fixture.AdminExecuteAsync("UPDATE messaging.event_deliveries SET status = 'processing', attempts = 1, locked_until = now() + interval '1 minute' WHERE event_id = @e", ("e", eventId));

        await harness.Processor.ProcessOnceAsync(CancellationToken.None);
        Assert.Empty(harness.Probe.Received);

        await fixture.AdminExecuteAsync("UPDATE messaging.event_deliveries SET locked_until = now() - interval '1 second' WHERE event_id = @e", ("e", eventId));
        await harness.Processor.ProcessOnceAsync(CancellationToken.None);

        Assert.Single(harness.Probe.Received);
        Assert.Equal("delivered", await StatusAsync(harness.ConsumerName, eventId));
        Assert.Equal(2, await AttemptsAsync(harness.ConsumerName, eventId));
    }

    [Fact]
    public async Task Only_deliveries_of_consumers_registered_in_this_process_are_claimed()
    {
        var eventType = $"test.foreign.{Guid.NewGuid():N}.v1";
        await using var owner = new MessagingHarness(fixture, new HashSet<string> { eventType });
        var (_, eventId) = await fixture.InsertOutboxAsync("crm", TestTenants.Next(), eventType);
        await owner.Relay.RelayOnceAsync(CancellationToken.None);

        await using var stranger = new MessagingHarness(fixture, new HashSet<string> { "test.unrelated.v1" });
        await stranger.Processor.ProcessOnceAsync(CancellationToken.None);

        Assert.Equal("pending", await StatusAsync(owner.ConsumerName, eventId));
        Assert.Equal(0, await AttemptsAsync(owner.ConsumerName, eventId));
    }

    [Fact]
    public void A_database_error_is_described_by_its_sqlstate_and_nothing_else()
    {
        var error = new PostgresException("duplicate key value violates unique constraint", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation,
            detail: "Key (email)=(someone@example.com) already exists.");

        Assert.Equal("PostgresException 23505", DeliveryProcessor.Describe(error));
        Assert.Equal("TimeoutException", DeliveryProcessor.Describe(new TimeoutException("secret")));
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(5, 80)]
    [InlineData(8, 640)]
    [InlineData(9, 900)]
    [InlineData(30, 900)]
    public void Backs_off_exponentially_up_to_fifteen_minutes(int attempts, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), DeliveryProcessor.Backoff(attempts));
}
