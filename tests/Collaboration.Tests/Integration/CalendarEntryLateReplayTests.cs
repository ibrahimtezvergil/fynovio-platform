using System.Data.Common;
using Collaboration.Application;
using Collaboration.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Collaboration.Tests.Integration;

/// <summary>The window between a handler's idempotency lookup and its load of the entry: if the request that owns the key
/// commits inside it, the retry must replay that result, not report a conflict or a missing entry. Forced deterministically
/// by running the winner just before the loser's entry query.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class CalendarEntryLateReplayTests(PostgresFixture fixture)
{
    private readonly Harness _harness = new(fixture);

    private async Task<(TenantId Tenant, long Id)> SeedAsync()
    {
        var tenant = TestData.NextTenant();
        var created = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, $"seed-{Guid.NewGuid():N}", title: "Original"));
        return (tenant, created.Id);
    }

    private async Task<CollaborationDbContext> ContextWithAsync(BeforeEntryLoad interceptor)
    {
        var options = new DbContextOptionsBuilder<CollaborationDbContext>()
            .UseNpgsql(await fixture.RuntimeConnectionStringAsync(),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CollaborationDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(interceptor)
            .Options;
        return new CollaborationDbContext(options);
    }

    [Fact]
    public async Task An_update_whose_key_owner_commits_before_the_entry_load_replays_instead_of_conflicting()
    {
        var (tenant, id) = await SeedAsync();
        var command = Commands.Update(tenant, id, Harness.Alice, 1, "k-late-update", title: "Once");
        var winner = new BeforeEntryLoad(() => _harness.UpdateAsync(command with { CorrelationId = Guid.NewGuid() }));
        await using var context = await ContextWithAsync(winner);

        var result = await new UpdateCalendarEntryHandler(context, StubAuthorizer.AlwaysAllow, StubLinkDirectory.AllowAll()).HandleAsync(command);

        Assert.True(winner.Fired);
        Assert.True(result.Replayed);
        Assert.Equal(2, result.RowVersion);
        Assert.Equal(2, (await _harness.ReadEntryAsync(tenant, id))!.RowVersion);
        Assert.Equal(2, await _harness.CountOutboxAsync(tenant));
        Assert.Equal(2, await _harness.CountIdempotencyAsync(tenant));
    }

    [Fact]
    public async Task A_delete_whose_key_owner_commits_before_the_entry_load_replays_instead_of_reporting_not_found()
    {
        var (tenant, id) = await SeedAsync();
        var command = Commands.Delete(tenant, id, Harness.Alice, 1, "k-late-delete");
        var winner = new BeforeEntryLoad(() => _harness.DeleteAsync(command with { CorrelationId = Guid.NewGuid() }));
        await using var context = await ContextWithAsync(winner);

        var result = await new DeleteCalendarEntryHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        Assert.True(winner.Fired);
        Assert.True(result.Replayed);
        Assert.Null(await _harness.ReadEntryAsync(tenant, id));
        Assert.Equal(2, await _harness.CountOutboxAsync(tenant));
        Assert.Equal(2, await _harness.CountIdempotencyAsync(tenant));
    }

    [Fact]
    public async Task A_late_winner_that_used_the_key_for_a_different_body_is_a_reuse_conflict_not_a_replay()
    {
        var (tenant, id) = await SeedAsync();
        var command = Commands.Update(tenant, id, Harness.Alice, 1, "k-late-reuse", title: "Mine");
        var winner = new BeforeEntryLoad(() => _harness.UpdateAsync(command with { Title = "Someone else's body" }));
        await using var context = await ContextWithAsync(winner);

        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() =>
            new UpdateCalendarEntryHandler(context, StubAuthorizer.AlwaysAllow, StubLinkDirectory.AllowAll()).HandleAsync(command));

        Assert.Equal("Someone else's body", (await _harness.ReadEntryAsync(tenant, id))!.Title);
    }

    [Fact]
    public async Task A_late_winner_under_a_different_key_is_still_a_plain_concurrency_conflict()
    {
        var (tenant, id) = await SeedAsync();
        var command = Commands.Update(tenant, id, Harness.Alice, 1, "k-mine", title: "Mine");
        var winner = new BeforeEntryLoad(() => _harness.UpdateAsync(Commands.Update(tenant, id, Harness.Alice, 1, "k-theirs", title: "Theirs")));
        await using var context = await ContextWithAsync(winner);

        await Assert.ThrowsAsync<CalendarEntryConcurrencyConflictException>(() =>
            new UpdateCalendarEntryHandler(context, StubAuthorizer.AlwaysAllow, StubLinkDirectory.AllowAll()).HandleAsync(command));

        Assert.Equal("Theirs", (await _harness.ReadEntryAsync(tenant, id))!.Title);
        Assert.Equal(2, await _harness.CountIdempotencyAsync(tenant));
    }

    [Fact]
    public async Task A_delete_that_lost_to_a_delete_under_a_different_key_is_a_plain_not_found()
    {
        var (tenant, id) = await SeedAsync();
        var winner = new BeforeEntryLoad(() => _harness.DeleteAsync(Commands.Delete(tenant, id, Harness.Alice, 1, "k-theirs")));
        await using var context = await ContextWithAsync(winner);

        await Assert.ThrowsAsync<CalendarEntryNotFoundException>(() =>
            new DeleteCalendarEntryHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(Commands.Delete(tenant, id, Harness.Alice, 1, "k-mine")));

        Assert.Equal(2, await _harness.CountIdempotencyAsync(tenant));
    }

    /// <summary>Runs `winner` to completion, once, right before the handler's first query against the entries table.</summary>
    private sealed class BeforeEntryLoad(Func<Task> winner) : DbCommandInterceptor
    {
        public bool Fired { get; private set; }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!Fired && command.CommandText.Contains("calendar_entries", StringComparison.Ordinal))
            {
                Fired = true;
                await winner();
            }

            return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
