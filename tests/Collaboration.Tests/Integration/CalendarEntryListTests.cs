using Collaboration.Application;
using Collaboration.Domain;
using Contracts;
using Xunit;

namespace Collaboration.Tests.Integration;

/// <summary>List semantics: window overlap with exclusive ends, the one-day all-day widening, privacy,
/// bounded range and result cap. Rows are seeded through the admin role; reads run as the runtime role.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class CalendarEntryListTests(PostgresFixture fixture)
{
    private readonly Harness _harness = new(fixture);

    private static readonly DateTimeOffset From = new(2026, 3, 10, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 3, 10, 18, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset At(int hour, int minute = 0) => new(2026, 3, 10, hour, minute, 0, TimeSpan.Zero);

    private async Task SeedAsync(params CalendarEntry[] entries)
    {
        await using var context = fixture.CreateAdminContext();
        context.CalendarEntries.AddRange(entries);
        await context.SaveChangesAsync();
    }

    private static CalendarEntry Timed(TenantId tenant, PrincipalRef owner, string title, DateTimeOffset start, DateTimeOffset? end) =>
        CalendarEntry.Create(tenant, owner, title, null, "#336699", false, start, end, null, null, null);

    private static CalendarEntry AllDay(TenantId tenant, PrincipalRef owner, string title, DateOnly start, DateOnly end) =>
        CalendarEntry.Create(tenant, owner, title, null, "#336699", true, null, null, start, end, null);

    private static string[] Titles(IEnumerable<CalendarEntryDto> entries) => entries.Select(e => e.Title).Order().ToArray();

    [Fact]
    public async Task Timed_entries_overlapping_the_window_are_returned_and_exclusive_edges_are_not()
    {
        var tenant = TestData.NextTenant();
        var owner = Harness.Alice;
        await SeedAsync(Timed(tenant, owner, "inside", At(12), At(13)),
            Timed(tenant, owner, "before", At(8), At(9)),
            Timed(tenant, owner, "after", At(19), At(20)),
            Timed(tenant, owner, "straddles-start", At(9), At(11)),
            Timed(tenant, owner, "straddles-end", At(17), At(19)),
            Timed(tenant, owner, "spans-window", At(8), At(20)),
            Timed(tenant, owner, "ends-exactly-at-from", At(9), At(10)),
            Timed(tenant, owner, "starts-exactly-at-to", At(18), At(19)),
            Timed(tenant, owner, "starts-exactly-at-from", At(10), At(11)),
            Timed(tenant, owner, "ends-exactly-at-to", At(17), At(18)));

        var result = await _harness.ListAsync(tenant, owner, From, To);

        Assert.Equal(
            new[] { "ends-exactly-at-to", "inside", "spans-window", "starts-exactly-at-from", "straddles-end", "straddles-start" },
            Titles(result));
    }

    [Fact]
    public async Task A_timed_entry_without_an_end_appears_only_when_it_starts_inside_the_window()
    {
        var tenant = TestData.NextTenant();
        var owner = Harness.Alice;
        await SeedAsync(Timed(tenant, owner, "point-long-before", At(9), null),
            Timed(tenant, owner, "point-just-before", At(9, 59), null),
            Timed(tenant, owner, "point-at-from", At(10), null),
            Timed(tenant, owner, "point-inside", At(11), null),
            Timed(tenant, owner, "point-at-to", At(18), null),
            Timed(tenant, owner, "point-after", At(19), null));

        var result = await _harness.ListAsync(tenant, owner, From, To);

        Assert.Equal(new[] { "point-at-from", "point-inside" }, Titles(result));
    }

    [Fact]
    public async Task All_day_entries_follow_the_one_day_widening_rule_with_exclusive_ends()
    {
        // The window is 2026-03-10 UTC, widened to [2026-03-09, 2026-03-11): an all-day entry is returned when
        // start_date < 2026-03-11 AND end_date > 2026-03-09 (its exclusive end must be after the widened start).
        var tenant = TestData.NextTenant();
        var owner = Harness.Alice;
        await SeedAsync(AllDay(tenant, owner, "on-the-day", new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 11)),
            AllDay(tenant, owner, "multi-day-across", new DateOnly(2026, 3, 8), new DateOnly(2026, 3, 13)),
            AllDay(tenant, owner, "day-before-widened-in", new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 10)),
            AllDay(tenant, owner, "ends-day-after-widened-in", new DateOnly(2026, 3, 8), new DateOnly(2026, 3, 10)),
            AllDay(tenant, owner, "ends-exactly-at-widened-start", new DateOnly(2026, 3, 8), new DateOnly(2026, 3, 9)),
            AllDay(tenant, owner, "starts-exactly-at-widened-end", new DateOnly(2026, 3, 11), new DateOnly(2026, 3, 12)),
            AllDay(tenant, owner, "far-before", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 2)),
            AllDay(tenant, owner, "far-after", new DateOnly(2026, 3, 20), new DateOnly(2026, 3, 21)));

        var result = await _harness.ListAsync(tenant, owner, From, To);

        Assert.Equal(
            new[] { "day-before-widened-in", "ends-day-after-widened-in", "multi-day-across", "on-the-day" },
            Titles(result));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-5)]
    [InlineData(0)]
    public async Task A_window_expressed_with_any_offset_selects_the_same_instants(int offsetHours)
    {
        var tenant = TestData.NextTenant();
        var owner = Harness.Alice;
        await SeedAsync(Timed(tenant, owner, "inside", At(12), At(13)),
            Timed(tenant, owner, "before", At(8), At(9)),
            Timed(tenant, owner, "point-inside", At(17), null));
        var offset = TimeSpan.FromHours(offsetHours);

        var result = await _harness.ListAsync(tenant, owner, From.ToOffset(offset), To.ToOffset(offset));

        Assert.Equal(new[] { "inside", "point-inside" }, Titles(result));
    }

    [Fact]
    public async Task Entries_of_other_owners_and_other_tenants_are_excluded()
    {
        var tenant = TestData.NextTenant();
        var otherTenant = TestData.NextTenant();
        await SeedAsync(Timed(tenant, Harness.Alice, "alice-mine", At(12), At(13)));
        await SeedAsync(Timed(tenant, Harness.Bob, "bob-same-tenant", At(12), At(13)));
        await SeedAsync(Timed(otherTenant, Harness.Alice, "alice-other-tenant", At(12), At(13)));

        var alice = await _harness.ListAsync(tenant, Harness.Alice, From, To);
        var bob = await _harness.ListAsync(tenant, Harness.Bob, From, To);
        var aliceElsewhere = await _harness.ListAsync(otherTenant, Harness.Alice, From, To);

        Assert.Equal(new[] { "alice-mine" }, Titles(alice));
        Assert.Equal(new[] { "bob-same-tenant" }, Titles(bob));
        Assert.Equal(new[] { "alice-other-tenant" }, Titles(aliceElsewhere));
    }

    [Fact]
    public async Task Ordering_is_deterministic_and_ties_break_by_id()
    {
        var tenant = TestData.NextTenant();
        var owner = Harness.Alice;
        await SeedAsync(Timed(tenant, owner, "late", At(15), At(16)),
            Timed(tenant, owner, "tie-first", At(12), At(13)),
            Timed(tenant, owner, "tie-second", At(12), At(14)),
            Timed(tenant, owner, "early", At(11), At(12)));

        var first = await _harness.ListAsync(tenant, owner, From, To);
        var second = await _harness.ListAsync(tenant, owner, From, To);

        Assert.Equal(new[] { "early", "tie-first", "tie-second", "late" }, first.Select(e => e.Title).ToArray());
        Assert.Equal(first.Select(e => e.Id), second.Select(e => e.Id));
    }

    [Fact]
    public async Task A_reversed_or_empty_range_is_a_validation_error()
    {
        var tenant = TestData.NextTenant();

        await Assert.ThrowsAsync<ArgumentException>(() => _harness.ListAsync(tenant, Harness.Alice, From, From));
        await Assert.ThrowsAsync<ArgumentException>(() => _harness.ListAsync(tenant, Harness.Alice, To, From));
    }

    [Fact]
    public async Task A_range_of_exactly_100_days_is_accepted_and_longer_is_too_large()
    {
        var tenant = TestData.NextTenant();

        var accepted = await _harness.ListAsync(tenant, Harness.Alice, From, From.AddDays(100));
        Assert.Empty(accepted);

        await Assert.ThrowsAsync<CalendarRangeTooLargeException>(() =>
            _harness.ListAsync(tenant, Harness.Alice, From, From.AddDays(100).AddTicks(1)));
    }

    [Fact]
    public async Task Exactly_500_matches_are_returned_and_a_501st_fails_instead_of_truncating()
    {
        var tenant = TestData.NextTenant();
        var owner = Harness.Alice;
        var dayStart = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero);
        await SeedAsync(Enumerable.Range(0, 500).Select(i => Timed(tenant, owner, $"e{i}", dayStart.AddMinutes(i), null)).ToArray());

        var result = await _harness.ListAsync(tenant, owner, dayStart, dayStart.AddDays(1));
        Assert.Equal(500, result.Count);

        await SeedAsync(Timed(tenant, owner, "e500", dayStart.AddMinutes(500), null));
        await Assert.ThrowsAsync<CalendarRangeTooLargeException>(() =>
            _harness.ListAsync(tenant, owner, dayStart, dayStart.AddDays(1)));
    }

    [Fact]
    public async Task A_denied_list_throws_the_typed_exception()
    {
        var tenant = TestData.NextTenant();

        var ex = await Assert.ThrowsAsync<CalendarEntryAuthorizationDeniedException>(() =>
            _harness.ListAsync(tenant, Harness.Alice, From, To, StubAuthorizer.AlwaysDeny));

        Assert.Equal("collaboration.calendar_entry.list", ex.ActionKey);
        Assert.Equal(AuthorizationDenialStage.Coarse, ex.DenialStage);
    }

    [Fact]
    public async Task Listing_returns_the_dto_shape_for_timed_and_all_day_entries()
    {
        var tenant = TestData.NextTenant();
        var owner = Harness.Alice;
        await SeedAsync(Timed(tenant, owner, "timed", At(12), At(13)),
            AllDay(tenant, owner, "all-day", new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 11)));

        var result = await _harness.ListAsync(tenant, owner, From, To);

        var timed = Assert.Single(result, e => e.Title == "timed");
        Assert.False(timed.AllDay);
        Assert.Equal(At(12), timed.StartAt);
        Assert.Null(timed.StartDate);
        var allDay = Assert.Single(result, e => e.Title == "all-day");
        Assert.True(allDay.AllDay);
        Assert.Equal(new DateOnly(2026, 3, 10), allDay.StartDate);
        Assert.Null(allDay.StartAt);
    }
}
