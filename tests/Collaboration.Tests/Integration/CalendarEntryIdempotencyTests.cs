using Collaboration.Application;
using Collaboration.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Collaboration.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class CalendarEntryIdempotencyTests(PostgresFixture fixture)
{
    private readonly Harness _harness = new(fixture);

    [Fact]
    public async Task Replay_returns_the_same_entry_and_leaves_exactly_one_entry_and_one_outbox_row()
    {
        var tenant = TestData.NextTenant();
        var command = Commands.Timed(tenant, Harness.Alice, "k-replay", title: "Original", notes: "n");

        var first = await _harness.CreateAsync(command);
        var second = await _harness.CreateAsync(command with { CorrelationId = Guid.NewGuid() });

        Assert.False(first.Replayed);
        Assert.True(second.Replayed);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.RowVersion, second.RowVersion);
        Assert.Equal(1, await _harness.CountEntriesAsync(tenant));
        Assert.Equal(1, await _harness.CountOutboxAsync(tenant));
        Assert.Equal(1, await _harness.CountIdempotencyAsync(tenant));
    }

    [Fact]
    public async Task Same_key_with_different_title_is_a_reuse_conflict()
    {
        var tenant = TestData.NextTenant();
        await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-title", title: "A"));

        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() =>
            _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-title", title: "B")));

        Assert.Equal(1, await _harness.CountEntriesAsync(tenant));
    }

    [Fact]
    public async Task Same_key_with_different_notes_is_a_reuse_conflict()
    {
        var tenant = TestData.NextTenant();
        await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-notes", notes: "first"));

        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() =>
            _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-notes", notes: "second")));
        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() =>
            _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-notes", notes: null)));

        Assert.Equal(1, await _harness.CountEntriesAsync(tenant));
    }

    [Fact]
    public async Task Same_key_with_different_color_is_a_reuse_conflict()
    {
        var tenant = TestData.NextTenant();
        await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-color", color: "#336699"));

        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() =>
            _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-color", color: "#996633")));

        Assert.Equal(1, await _harness.CountEntriesAsync(tenant));
    }

    [Fact]
    public async Task Same_key_with_different_link_or_timing_is_a_reuse_conflict()
    {
        var tenant = TestData.NextTenant();
        await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-other"));

        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() =>
            _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-other", start: Commands.Noon.AddMinutes(1))));
        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() =>
            _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-other", end: Commands.Noon.AddHours(1))));
        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() =>
            _harness.CreateAsync(Commands.Timed(
                tenant, Harness.Alice, "k-other", link: new EntityRef(tenant, "crm", "opportunity", 7))));
    }

    [Fact]
    public async Task Field_values_containing_the_separator_do_not_collide()
    {
        var tenant = TestData.NextTenant();
        await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-sep", title: "a|b", notes: "c"));

        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() =>
            _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-sep", title: "a", notes: "b|c")));
    }

    [Fact]
    public async Task The_same_instant_written_with_a_different_offset_is_a_replay()
    {
        var tenant = TestData.NextTenant();
        var utc = Commands.Timed(tenant, Harness.Alice, "k-offset", start: Commands.Noon, end: Commands.Noon.AddHours(1));
        var plusThree = utc with { StartAt = Commands.Noon.ToOffset(TimeSpan.FromHours(3)), EndAt = Commands.Noon.AddHours(1).ToOffset(TimeSpan.FromHours(3)) };

        var first = await _harness.CreateAsync(utc);
        var second = await _harness.CreateAsync(plusThree);

        Assert.True(second.Replayed);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await _harness.CountEntriesAsync(tenant));
    }

    [Fact]
    public async Task A_colour_written_in_a_different_case_is_a_replay_because_the_aggregate_normalizes_it()
    {
        var tenant = TestData.NextTenant();
        var first = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-case", color: "#AABBCC"));
        var second = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-case", color: "#aabbcc"));

        Assert.True(second.Replayed);
        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task The_same_key_is_independent_across_principals_and_tenants()
    {
        var tenantA = TestData.NextTenant();
        var tenantB = TestData.NextTenant();

        var aliceA = await _harness.CreateAsync(Commands.Timed(tenantA, Harness.Alice, "shared-key"));
        var bobA = await _harness.CreateAsync(Commands.Timed(tenantA, Harness.Bob, "shared-key"));
        var aliceB = await _harness.CreateAsync(Commands.Timed(tenantB, Harness.Alice, "shared-key"));

        Assert.All(new[] { aliceA, bobA, aliceB }, r => Assert.False(r.Replayed));
        Assert.Equal(3, new[] { aliceA.Id, bobA.Id, aliceB.Id }.Distinct().Count());
        Assert.Equal(2, await _harness.CountEntriesAsync(tenantA));
        Assert.Equal(1, await _harness.CountEntriesAsync(tenantB));
    }

    [Fact]
    public async Task Concurrent_requests_with_the_same_key_create_exactly_one_entry()
    {
        var tenant = TestData.NextTenant();
        const int parallelism = 12;
        var start = new TaskCompletionSource();
        var command = Commands.Timed(tenant, Harness.Alice, "k-race", title: "Raced");

        var calls = Enumerable.Range(0, parallelism).Select(async _ =>
        {
            await start.Task;
            return await _harness.CreateAsync(command with { CorrelationId = Guid.NewGuid() });
        }).ToArray();
        start.SetResult();
        var results = await Task.WhenAll(calls);

        Assert.Single(results.Select(r => r.Id).Distinct());
        Assert.Equal(1, results.Count(r => !r.Replayed));
        Assert.Equal(parallelism - 1, results.Count(r => r.Replayed));
        Assert.Equal(1, await _harness.CountEntriesAsync(tenant));
        Assert.Equal(1, await _harness.CountOutboxAsync(tenant));
        Assert.Equal(1, await _harness.CountIdempotencyAsync(tenant));
    }

    [Fact]
    public async Task A_failure_in_the_second_save_leaves_no_entry_outbox_or_idempotency_row_and_the_retry_succeeds()
    {
        var tenant = TestData.NextTenant();
        var command = Commands.Timed(tenant, Harness.Alice, "k-atomic");
        var options = new DbContextOptionsBuilder<CollaborationDbContext>()
            .UseNpgsql(await fixture.RuntimeConnectionStringAsync(),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CollaborationDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new FailOnSaveNumber(2))
            .Options;

        await using (var failing = new CollaborationDbContext(options))
        {
            var handler = new CreateCalendarEntryHandler(failing, StubAuthorizer.AlwaysAllow);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));
            Assert.Equal(FailOnSaveNumber.Message, ex.Message);
        }

        Assert.Equal(0, await _harness.CountEntriesAsync(tenant));
        Assert.Equal(0, await _harness.CountOutboxAsync(tenant));
        Assert.Equal(0, await _harness.CountIdempotencyAsync(tenant));

        var retry = await _harness.CreateAsync(command);
        Assert.False(retry.Replayed);
        Assert.Equal(1, await _harness.CountEntriesAsync(tenant));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_key_is_rejected_before_any_write(string key)
    {
        var tenant = TestData.NextTenant();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, key)));

        Assert.Equal(0, await _harness.CountEntriesAsync(tenant));
    }

    [Fact]
    public async Task A_key_of_exactly_128_characters_is_accepted_and_129_is_rejected()
    {
        var tenant = TestData.NextTenant();

        var accepted = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, new string('k', 128)));
        Assert.False(accepted.Replayed);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, new string('k', 129))));
        Assert.Equal(1, await _harness.CountEntriesAsync(tenant));
    }

    /// <summary>Fails the Nth SaveChanges of a context; the handler saves twice (entry, then outbox + idempotency).</summary>
    private sealed class FailOnSaveNumber(int failingSave) : SaveChangesInterceptor
    {
        public const string Message = "injected save failure";
        private int _saves;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            Interlocked.Increment(ref _saves) == failingSave
                ? throw new InvalidOperationException(Message)
                : base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
