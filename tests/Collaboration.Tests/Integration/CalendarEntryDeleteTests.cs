using System.Text.Json;
using Collaboration.Application;
using Collaboration.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Collaboration.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class CalendarEntryDeleteTests(PostgresFixture fixture)
{
    private readonly Harness _harness = new(fixture);

    private async Task<(TenantId Tenant, long Id)> SeedAsync(string title = "Original", string? notes = "orig notes")
    {
        var tenant = TestData.NextTenant();
        var created = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, $"seed-{Guid.NewGuid():N}", title: title, notes: notes));
        return (tenant, created.Id);
    }

    [Fact]
    public async Task Delete_removes_the_row_and_records_a_thin_deleted_fact_in_the_same_transaction()
    {
        var (tenant, id) = await SeedAsync(title: "Secret title", notes: "Secret notes");

        var result = await _harness.DeleteAsync(Commands.Delete(tenant, id, Harness.Alice, 1, "k-delete"));

        Assert.False(result.Replayed);
        Assert.Null(await _harness.ReadEntryAsync(tenant, id));
        Assert.Null(await _harness.GetAsync(tenant, id, Harness.Alice));
        var outbox = await _harness.ReadOutboxAsync(tenant);
        Assert.Equal(2, outbox.Count);
        var deleted = outbox[1];
        Assert.Equal(CalendarEntryOutboxEvents.Deleted, deleted.EventType);
        Assert.Equal(id, deleted.AggregateId);
        Assert.Equal(2, deleted.AggregateVersion);
        Assert.Equal($"calendar-entries/{id}", deleted.Subject);
        using var payload = JsonDocument.Parse(deleted.Payload);
        Assert.Equal(id, payload.RootElement.GetProperty("EntryId").GetInt64());
        Assert.Equal("alice", payload.RootElement.GetProperty("OwnerPrincipalSubject").GetString());
        Assert.Equal(2, payload.RootElement.GetProperty("Version").GetInt64());
        Assert.DoesNotContain("secret", deleted.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, await _harness.CountIdempotencyAsync(tenant));
    }

    [Fact]
    public async Task Replaying_a_delete_returns_the_original_success_even_though_the_entry_is_gone()
    {
        var (tenant, id) = await SeedAsync();
        var command = Commands.Delete(tenant, id, Harness.Alice, 1, "k-replay");

        var first = await _harness.DeleteAsync(command);
        var second = await _harness.DeleteAsync(command with { CorrelationId = Guid.NewGuid() });

        Assert.False(first.Replayed);
        Assert.True(second.Replayed);
        Assert.Equal(2, await _harness.CountOutboxAsync(tenant));
        Assert.Equal(2, await _harness.CountIdempotencyAsync(tenant));
    }

    [Fact]
    public async Task Deleting_again_with_a_new_key_is_not_found()
    {
        var (tenant, id) = await SeedAsync();
        await _harness.DeleteAsync(Commands.Delete(tenant, id, Harness.Alice, 1, "k-first"));

        await Assert.ThrowsAsync<CalendarEntryNotFoundException>(() =>
            _harness.DeleteAsync(Commands.Delete(tenant, id, Harness.Alice, 1, "k-second")));

        Assert.Equal(2, await _harness.CountIdempotencyAsync(tenant));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(99)]
    public async Task A_stale_or_wrong_expected_version_conflicts_and_keeps_the_entry(long expectedVersion)
    {
        var (tenant, id) = await SeedAsync();

        await Assert.ThrowsAsync<CalendarEntryConcurrencyConflictException>(() =>
            _harness.DeleteAsync(Commands.Delete(tenant, id, Harness.Alice, expectedVersion, "k-stale")));

        Assert.NotNull(await _harness.ReadEntryAsync(tenant, id));
        Assert.Equal(1, await _harness.CountOutboxAsync(tenant));
        Assert.Equal(1, await _harness.CountIdempotencyAsync(tenant));
    }

    [Fact]
    public async Task Reusing_a_key_for_a_different_version_or_entry_is_a_reuse_conflict()
    {
        var (tenant, id) = await SeedAsync();
        var other = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "seed-other"));
        await _harness.DeleteAsync(Commands.Delete(tenant, id, Harness.Alice, 1, "k-reuse"));

        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() =>
            _harness.DeleteAsync(Commands.Delete(tenant, id, Harness.Alice, 2, "k-reuse")));
        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() =>
            _harness.DeleteAsync(Commands.Delete(tenant, other.Id, Harness.Alice, 1, "k-reuse")));

        Assert.NotNull(await _harness.ReadEntryAsync(tenant, other.Id));
    }

    [Fact]
    public async Task Concurrent_requests_with_the_same_key_delete_exactly_once()
    {
        var (tenant, id) = await SeedAsync();
        const int parallelism = 12;
        var start = new TaskCompletionSource();
        var command = Commands.Delete(tenant, id, Harness.Alice, 1, "k-race");

        var calls = Enumerable.Range(0, parallelism).Select(async _ =>
        {
            await start.Task;
            return await _harness.DeleteAsync(command with { CorrelationId = Guid.NewGuid() });
        }).ToArray();
        start.SetResult();
        var results = await Task.WhenAll(calls);

        Assert.Equal(1, results.Count(r => !r.Replayed));
        Assert.Equal(parallelism - 1, results.Count(r => r.Replayed));
        Assert.Null(await _harness.ReadEntryAsync(tenant, id));
        Assert.Equal(2, await _harness.CountOutboxAsync(tenant));
        Assert.Equal(2, await _harness.CountIdempotencyAsync(tenant));
    }

    [Fact]
    public async Task A_delete_racing_an_update_on_the_same_version_lets_exactly_one_win_and_leaves_a_consistent_state()
    {
        for (var round = 0; round < 6; round++)
        {
            var (tenant, id) = await SeedAsync();
            var start = new TaskCompletionSource();

            async Task<(bool Succeeded, Exception? Error)> Run(Func<Task> action)
            {
                await start.Task;
                try
                {
                    await action();
                    return (true, null);
                }
                catch (Exception ex)
                {
                    return (false, ex);
                }
            }

            var delete = Run(() => _harness.DeleteAsync(Commands.Delete(tenant, id, Harness.Alice, 1, "k-del")));
            var update = Run(() => _harness.UpdateAsync(Commands.Update(tenant, id, Harness.Alice, 1, "k-upd", title: "Racing")));
            start.SetResult();
            var (deleteResult, updateResult) = (await delete, await update);

            Assert.True(deleteResult.Succeeded ^ updateResult.Succeeded, "exactly one of the two commands must win");
            var loser = deleteResult.Succeeded ? updateResult.Error! : deleteResult.Error!;
            Assert.True(
                loser is CalendarEntryConcurrencyConflictException or CalendarEntryNotFoundException,
                $"the loser must see a conflict or not-found, not {loser.GetType().Name}");
            var stored = await _harness.ReadEntryAsync(tenant, id);
            if (deleteResult.Succeeded)
            {
                Assert.Null(stored);
            }
            else
            {
                Assert.Equal(2, stored!.RowVersion);
                Assert.Equal("Racing", stored.Title);
            }

            // create + exactly one more fact, and only the winner's idempotency row.
            Assert.Equal(2, await _harness.CountOutboxAsync(tenant));
            Assert.Equal(2, await _harness.CountIdempotencyAsync(tenant));
        }
    }

    [Fact]
    public async Task Another_owner_another_tenant_and_an_unknown_id_are_not_found_and_leave_no_trace()
    {
        var (tenant, id) = await SeedAsync();
        var otherTenant = TestData.NextTenant();

        await Assert.ThrowsAsync<CalendarEntryNotFoundException>(() =>
            _harness.DeleteAsync(Commands.Delete(tenant, id, Harness.Bob, 1, "k-bob")));
        await Assert.ThrowsAsync<CalendarEntryNotFoundException>(() =>
            _harness.DeleteAsync(Commands.Delete(otherTenant, id, Harness.Alice, 1, "k-tenant")));
        await Assert.ThrowsAsync<CalendarEntryNotFoundException>(() =>
            _harness.DeleteAsync(Commands.Delete(tenant, id + 1_000_000, Harness.Alice, 1, "k-unknown")));

        Assert.NotNull(await _harness.ReadEntryAsync(tenant, id));
        Assert.Equal(1, await _harness.CountOutboxAsync(tenant));
        Assert.Equal(1, await _harness.CountIdempotencyAsync(tenant));
        Assert.Equal(0, await _harness.CountIdempotencyAsync(otherTenant));
    }

    [Fact]
    public async Task A_coarse_denial_wins_over_not_found_and_a_record_denial_names_the_entry()
    {
        var (tenant, id) = await SeedAsync();

        var coarse = await Assert.ThrowsAsync<CalendarEntryAuthorizationDeniedException>(() =>
            _harness.DeleteAsync(Commands.Delete(tenant, id + 1_000_000, Harness.Alice, 1, "k-coarse"), StubAuthorizer.AlwaysDeny));
        var record = await Assert.ThrowsAsync<CalendarEntryAuthorizationDeniedException>(() =>
            _harness.DeleteAsync(Commands.Delete(tenant, id, Harness.Alice, 1, "k-record"), new CalendarEntryUpdateTests.DenyResourceLevelAuthorizer()));

        Assert.Equal(AuthorizationDenialStage.Coarse, coarse.DenialStage);
        Assert.Null(coarse.EntryId);
        Assert.Equal(AuthorizationDenialStage.Record, record.DenialStage);
        Assert.Equal(id, record.EntryId);
        Assert.NotNull(await _harness.ReadEntryAsync(tenant, id));
        Assert.Equal(1, await _harness.CountIdempotencyAsync(tenant));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_key_is_rejected_before_any_write(string key)
    {
        var (tenant, id) = await SeedAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _harness.DeleteAsync(Commands.Delete(tenant, id, Harness.Alice, 1, key)));

        Assert.NotNull(await _harness.ReadEntryAsync(tenant, id));
    }

    [Fact]
    public async Task A_failure_after_the_writes_but_before_commit_keeps_the_entry_and_records_nothing()
    {
        var (tenant, id) = await SeedAsync();
        var options = new DbContextOptionsBuilder<CollaborationDbContext>()
            .UseNpgsql(await fixture.RuntimeConnectionStringAsync(),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CollaborationDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new CalendarEntryUpdateTests.FailAfterSave())
            .Options;

        await using (var failing = new CollaborationDbContext(options))
        {
            var handler = new DeleteCalendarEntryHandler(failing, StubAuthorizer.AlwaysAllow);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.HandleAsync(Commands.Delete(tenant, id, Harness.Alice, 1, "k-atomic")));
            Assert.Equal(CalendarEntryUpdateTests.FailAfterSave.Message, ex.Message);
        }

        Assert.NotNull(await _harness.ReadEntryAsync(tenant, id));
        Assert.Equal(1, await _harness.CountOutboxAsync(tenant));
        Assert.Equal(1, await _harness.CountIdempotencyAsync(tenant));

        var retry = await _harness.DeleteAsync(Commands.Delete(tenant, id, Harness.Alice, 1, "k-atomic"));
        Assert.False(retry.Replayed);
        Assert.Null(await _harness.ReadEntryAsync(tenant, id));
    }
}
