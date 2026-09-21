using System.Text.Json;
using Collaboration.Application;
using Collaboration.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Collaboration.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class CalendarEntryUpdateTests(PostgresFixture fixture)
{
    private readonly Harness _harness = new(fixture);

    private async Task<(TenantId Tenant, long Id)> SeedAsync(PrincipalRef? owner = null, EntityRef? link = null, string title = "Original", string? notes = "orig notes")
    {
        var tenant = TestData.NextTenant();
        var created = await _harness.CreateAsync(Commands.Timed(
            tenant, owner ?? Harness.Alice, $"seed-{Guid.NewGuid():N}", title: title, notes: notes, link: link ?? new EntityRef(tenant, "crm", "opportunity", 17)));
        return (tenant, created.Id);
    }

    [Fact]
    public async Task Update_replaces_every_field_bumps_the_version_and_clears_link_and_notes()
    {
        var (tenant, id) = await SeedAsync();

        var result = await _harness.UpdateAsync(new UpdateCalendarEntryCommand(
            tenant, id, Harness.Alice, 1, "Renamed", null, "#ABCDEF", true, null, null,
            new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), null, "k-update", Guid.NewGuid()));

        Assert.False(result.Replayed);
        Assert.Equal(id, result.Id);
        Assert.Equal(2, result.RowVersion);
        var stored = (await _harness.ReadEntryAsync(tenant, id))!;
        Assert.Equal("Renamed", stored.Title);
        Assert.Null(stored.Notes);
        Assert.Equal("#abcdef", stored.Color);
        Assert.True(stored.AllDay);
        Assert.Null(stored.StartAt);
        Assert.Null(stored.EndAt);
        Assert.Equal(new DateOnly(2026, 4, 1), stored.StartDate);
        Assert.Equal(new DateOnly(2026, 4, 3), stored.EndDate);
        Assert.Null(stored.Link);
        Assert.Equal(2, stored.RowVersion);
        Assert.Equal(Harness.Alice, stored.Owner);
        Assert.True(stored.UpdatedAt > stored.CreatedAt);

        var dto = await _harness.GetAsync(tenant, id, Harness.Alice);
        Assert.Equal(2, dto!.RowVersion);
    }

    [Fact]
    public async Task Update_can_switch_an_all_day_entry_to_a_timed_one_and_set_a_link()
    {
        var tenant = TestData.NextTenant();
        var created = await _harness.CreateAsync(Commands.AllDay(tenant, Harness.Alice, "seed", new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 2)));
        var link = new EntityRef(tenant, "crm", "opportunity", 9);

        await _harness.UpdateAsync(Commands.Update(tenant, created.Id, Harness.Alice, 1, "k-1", start: Commands.Noon, end: Commands.Noon.AddHours(1), link: link));

        var stored = (await _harness.ReadEntryAsync(tenant, created.Id))!;
        Assert.False(stored.AllDay);
        Assert.Null(stored.StartDate);
        Assert.Null(stored.EndDate);
        Assert.Equal(Commands.Noon, stored.StartAt);
        Assert.Equal(link, stored.Link);
    }

    [Fact]
    public async Task Update_with_offset_times_stores_the_same_instant_in_utc()
    {
        var (tenant, id) = await SeedAsync();
        var start = new DateTimeOffset(2026, 6, 1, 15, 30, 0, TimeSpan.FromHours(3));

        await _harness.UpdateAsync(Commands.Update(tenant, id, Harness.Alice, 1, "k-offset", start: start, end: start.AddHours(2)));

        var stored = (await _harness.ReadEntryAsync(tenant, id))!;
        Assert.Equal(start, stored.StartAt);
        Assert.Equal(TimeSpan.Zero, stored.StartAt!.Value.Offset);
    }

    [Fact]
    public async Task Update_records_a_thin_updated_fact_without_title_or_notes()
    {
        var (tenant, id) = await SeedAsync(title: "Secret title", notes: "Secret notes");

        await _harness.UpdateAsync(Commands.Update(tenant, id, Harness.Alice, 1, "k-outbox", title: "New secret title", notes: "New secret notes"));

        var outbox = await _harness.ReadOutboxAsync(tenant);
        Assert.Equal(2, outbox.Count);
        var updated = outbox[1];
        Assert.Equal(CalendarEntryOutboxEvents.Updated, updated.EventType);
        Assert.Equal(id, updated.AggregateId);
        Assert.Equal(2, updated.AggregateVersion);
        Assert.Equal($"calendar-entries/{id}", updated.Subject);
        using var payload = JsonDocument.Parse(updated.Payload);
        Assert.Equal(id, payload.RootElement.GetProperty("EntryId").GetInt64());
        Assert.Equal("alice", payload.RootElement.GetProperty("OwnerPrincipalSubject").GetString());
        Assert.DoesNotContain("secret", updated.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("notes", updated.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("title", updated.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, await _harness.CountIdempotencyAsync(tenant));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(99)]
    public async Task A_stale_or_wrong_expected_version_conflicts_and_changes_nothing(long expectedVersion)
    {
        var (tenant, id) = await SeedAsync();

        await Assert.ThrowsAsync<CalendarEntryConcurrencyConflictException>(() =>
            _harness.UpdateAsync(Commands.Update(tenant, id, Harness.Alice, expectedVersion, "k-stale")));

        var stored = (await _harness.ReadEntryAsync(tenant, id))!;
        Assert.Equal("Original", stored.Title);
        Assert.Equal(1, stored.RowVersion);
        Assert.Equal(1, await _harness.CountOutboxAsync(tenant));
        Assert.Equal(1, await _harness.CountIdempotencyAsync(tenant));
    }

    [Fact]
    public async Task Replay_returns_the_original_result_without_writing_again()
    {
        var (tenant, id) = await SeedAsync();
        var command = Commands.Update(tenant, id, Harness.Alice, 1, "k-replay", title: "Once");

        var first = await _harness.UpdateAsync(command);
        var second = await _harness.UpdateAsync(command with { CorrelationId = Guid.NewGuid() });

        Assert.False(first.Replayed);
        Assert.True(second.Replayed);
        Assert.Equal(first.RowVersion, second.RowVersion);
        Assert.Equal(2, (await _harness.ReadEntryAsync(tenant, id))!.RowVersion);
        Assert.Equal(2, await _harness.CountOutboxAsync(tenant));
        Assert.Equal(2, await _harness.CountIdempotencyAsync(tenant));
    }

    [Fact]
    public async Task A_retry_still_replays_after_the_entry_moved_on_through_another_key()
    {
        var (tenant, id) = await SeedAsync();
        var first = await _harness.UpdateAsync(Commands.Update(tenant, id, Harness.Alice, 1, "k-first", title: "First"));
        await _harness.UpdateAsync(Commands.Update(tenant, id, Harness.Alice, 2, "k-second", title: "Second"));

        var retry = await _harness.UpdateAsync(Commands.Update(tenant, id, Harness.Alice, 1, "k-first", title: "First"));

        Assert.True(retry.Replayed);
        Assert.Equal(first.RowVersion, retry.RowVersion);
        var stored = (await _harness.ReadEntryAsync(tenant, id))!;
        Assert.Equal("Second", stored.Title);
        Assert.Equal(3, stored.RowVersion);
    }

    [Fact]
    public async Task Reusing_a_key_with_a_different_body_or_version_is_a_reuse_conflict()
    {
        var (tenant, id) = await SeedAsync();
        await _harness.UpdateAsync(Commands.Update(tenant, id, Harness.Alice, 1, "k-reuse", title: "A"));

        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() =>
            _harness.UpdateAsync(Commands.Update(tenant, id, Harness.Alice, 1, "k-reuse", title: "B")));
        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() =>
            _harness.UpdateAsync(Commands.Update(tenant, id, Harness.Alice, 2, "k-reuse", title: "A")));
        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() =>
            _harness.UpdateAsync(Commands.Update(tenant, id, Harness.Alice, 1, "k-reuse", title: "A", notes: "different")));

        Assert.Equal("A", (await _harness.ReadEntryAsync(tenant, id))!.Title);
    }

    [Fact]
    public async Task Concurrent_updates_with_the_same_expected_version_let_exactly_one_win()
    {
        for (var round = 0; round < 6; round++)
        {
            var (tenant, id) = await SeedAsync();
            var start = new TaskCompletionSource();
            var attempts = new[] { "left", "right" }.Select(async name =>
            {
                await start.Task;
                try
                {
                    return (Result: (UpdateCalendarEntryResult?)await _harness.UpdateAsync(Commands.Update(tenant, id, Harness.Alice, 1, $"k-{name}", title: name)), Error: (Exception?)null);
                }
                catch (Exception ex)
                {
                    return (Result: null, Error: ex);
                }
            }).ToArray();
            start.SetResult();
            var outcomes = await Task.WhenAll(attempts);

            Assert.Single(outcomes, o => o.Result is not null);
            Assert.IsType<CalendarEntryConcurrencyConflictException>(Assert.Single(outcomes, o => o.Error is not null).Error);
            var stored = (await _harness.ReadEntryAsync(tenant, id))!;
            Assert.Equal(2, stored.RowVersion);
            Assert.Equal(outcomes.Single(o => o.Result is not null).Result!.RowVersion, stored.RowVersion);
            // create + the single winning update; the loser leaves neither an outbox fact nor an idempotency row.
            Assert.Equal(2, await _harness.CountOutboxAsync(tenant));
            Assert.Equal(2, await _harness.CountIdempotencyAsync(tenant));
        }
    }

    [Fact]
    public async Task Concurrent_requests_with_the_same_key_apply_the_update_exactly_once()
    {
        var (tenant, id) = await SeedAsync();
        const int parallelism = 12;
        var start = new TaskCompletionSource();
        var command = Commands.Update(tenant, id, Harness.Alice, 1, "k-race", title: "Raced");

        var calls = Enumerable.Range(0, parallelism).Select(async _ =>
        {
            await start.Task;
            return await _harness.UpdateAsync(command with { CorrelationId = Guid.NewGuid() });
        }).ToArray();
        start.SetResult();
        var results = await Task.WhenAll(calls);

        Assert.Equal(1, results.Count(r => !r.Replayed));
        Assert.Equal(parallelism - 1, results.Count(r => r.Replayed));
        Assert.Single(results.Select(r => r.RowVersion).Distinct());
        var stored = (await _harness.ReadEntryAsync(tenant, id))!;
        Assert.Equal(2, stored.RowVersion);
        Assert.Equal("Raced", stored.Title);
        Assert.Equal(2, await _harness.CountOutboxAsync(tenant));
        Assert.Equal(2, await _harness.CountIdempotencyAsync(tenant));
    }

    [Fact]
    public async Task Another_owner_another_tenant_and_an_unknown_id_are_not_found_and_leave_no_trace()
    {
        var (tenant, id) = await SeedAsync();
        var otherTenant = TestData.NextTenant();

        await Assert.ThrowsAsync<CalendarEntryNotFoundException>(() =>
            _harness.UpdateAsync(Commands.Update(tenant, id, Harness.Bob, 1, "k-bob")));
        await Assert.ThrowsAsync<CalendarEntryNotFoundException>(() =>
            _harness.UpdateAsync(Commands.Update(otherTenant, id, Harness.Alice, 1, "k-tenant")));
        await Assert.ThrowsAsync<CalendarEntryNotFoundException>(() =>
            _harness.UpdateAsync(Commands.Update(tenant, id + 1_000_000, Harness.Alice, 1, "k-unknown")));

        Assert.Equal("Original", (await _harness.ReadEntryAsync(tenant, id))!.Title);
        Assert.Equal(1, await _harness.CountOutboxAsync(tenant));
        Assert.Equal(1, await _harness.CountIdempotencyAsync(tenant));
        Assert.Equal(0, await _harness.CountIdempotencyAsync(otherTenant));
    }

    [Fact]
    public async Task A_coarse_denial_wins_over_not_found_and_changes_nothing()
    {
        var (tenant, id) = await SeedAsync();

        var existing = await Assert.ThrowsAsync<CalendarEntryAuthorizationDeniedException>(() =>
            _harness.UpdateAsync(Commands.Update(tenant, id, Harness.Alice, 1, "k-deny"), StubAuthorizer.AlwaysDeny));
        var missing = await Assert.ThrowsAsync<CalendarEntryAuthorizationDeniedException>(() =>
            _harness.UpdateAsync(Commands.Update(tenant, id + 1_000_000, Harness.Alice, 1, "k-deny-2"), StubAuthorizer.AlwaysDeny));

        Assert.Equal(AuthorizationDenialStage.Coarse, existing.DenialStage);
        Assert.Equal(AuthorizationDenialStage.Coarse, missing.DenialStage);
        Assert.Null(existing.EntryId);
        Assert.Equal("Original", (await _harness.ReadEntryAsync(tenant, id))!.Title);
        Assert.Equal(1, await _harness.CountIdempotencyAsync(tenant));
    }

    [Fact]
    public async Task A_record_level_denial_carries_the_entry_id_so_it_maps_to_not_found()
    {
        var (tenant, id) = await SeedAsync();

        var denied = await Assert.ThrowsAsync<CalendarEntryAuthorizationDeniedException>(() =>
            _harness.UpdateAsync(Commands.Update(tenant, id, Harness.Alice, 1, "k-record"), new DenyResourceLevelAuthorizer()));

        Assert.Equal(AuthorizationDenialStage.Record, denied.DenialStage);
        Assert.Equal(id, denied.EntryId);
        Assert.Equal("Original", (await _harness.ReadEntryAsync(tenant, id))!.Title);
        Assert.Equal(1, await _harness.CountOutboxAsync(tenant));
    }

    [Fact]
    public async Task A_domain_validation_failure_changes_nothing_and_consumes_no_key()
    {
        var (tenant, id) = await SeedAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _harness.UpdateAsync(Commands.Update(tenant, id, Harness.Alice, 1, "k-invalid", title: "   ")));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _harness.UpdateAsync(Commands.Update(tenant, id, Harness.Alice, 1, "k-invalid", color: "red")));

        Assert.Equal(1, (await _harness.ReadEntryAsync(tenant, id))!.RowVersion);
        Assert.Equal(1, await _harness.CountIdempotencyAsync(tenant));
        // The key was never consumed, so the corrected request goes through.
        var ok = await _harness.UpdateAsync(Commands.Update(tenant, id, Harness.Alice, 1, "k-invalid", title: "Fixed"));
        Assert.False(ok.Replayed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_key_is_rejected_before_any_write(string key)
    {
        var (tenant, id) = await SeedAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _harness.UpdateAsync(Commands.Update(tenant, id, Harness.Alice, 1, key)));

        Assert.Equal(1, (await _harness.ReadEntryAsync(tenant, id))!.RowVersion);
    }

    [Fact]
    public async Task A_failure_after_the_writes_but_before_commit_rolls_back_state_outbox_and_idempotency_together()
    {
        var (tenant, id) = await SeedAsync();
        var options = new DbContextOptionsBuilder<CollaborationDbContext>()
            .UseNpgsql(await fixture.RuntimeConnectionStringAsync(),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CollaborationDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new FailAfterSave())
            .Options;

        await using (var failing = new CollaborationDbContext(options))
        {
            var handler = new UpdateCalendarEntryHandler(failing, StubAuthorizer.AlwaysAllow);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.HandleAsync(Commands.Update(tenant, id, Harness.Alice, 1, "k-atomic", title: "Never")));
            Assert.Equal(FailAfterSave.Message, ex.Message);
        }

        var stored = (await _harness.ReadEntryAsync(tenant, id))!;
        Assert.Equal("Original", stored.Title);
        Assert.Equal(1, stored.RowVersion);
        Assert.Equal(1, await _harness.CountOutboxAsync(tenant));
        Assert.Equal(1, await _harness.CountIdempotencyAsync(tenant));

        var retry = await _harness.UpdateAsync(Commands.Update(tenant, id, Harness.Alice, 1, "k-atomic", title: "Now"));
        Assert.False(retry.Replayed);
    }

    /// <summary>Fails right after the SQL of a save ran, i.e. inside the still-open transaction.</summary>
    internal sealed class FailAfterSave : SaveChangesInterceptor
    {
        public const string Message = "injected failure after save";

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(Message);
    }

    /// <summary>Allows the create-shaped (coarse) check and denies once a concrete entry is being judged.</summary>
    internal sealed class DenyResourceLevelAuthorizer : IAuthorizer
    {
        public Task<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(request.Resource.Id is null
                ? new AuthorizationDecision(AuthorizationEffect.Allow, "coarse_allow", Guid.NewGuid(), 0, AuthorizationDenialStage.None)
                : new AuthorizationDecision(AuthorizationEffect.Deny, "record_deny", Guid.NewGuid(), 0, AuthorizationDenialStage.Record));
    }
}

/// <summary>Event-type names as the outbox stores them, spelled once for the update/delete tests.</summary>
internal static class CalendarEntryOutboxEvents
{
    public const string Updated = "enterprise.collaboration.calendar-entry.updated.v1";
    public const string Deleted = "enterprise.collaboration.calendar-entry.deleted.v1";
}
