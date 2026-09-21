using Collaboration.Domain;
using Contracts;
using Xunit;

namespace Collaboration.Tests.Domain;

public sealed class CalendarEntryTextAndPrecisionTests
{
    private static readonly TenantId Tenant = new(1);
    private static readonly PrincipalRef Owner = new("test", "owner");
    private static readonly DateTimeOffset Start = new(2026, 9, 21, 9, 0, 0, TimeSpan.Zero);

    private static CalendarEntry Timed(string title = "Call", string? notes = null, DateTimeOffset? start = null, DateTimeOffset? end = null) =>
        CalendarEntry.Create(Tenant, Owner, title, notes, "#123456", false, start ?? Start, end, null, null, null);

    // NUL, tab, vertical tab, form feed, a mid-range C0, the last C0, DEL, NEL, line separator, paragraph separator.
    public static TheoryData<char> ForbiddenTitleCharacters => new()
    {
        '\0', '\t', '\u000B', '\f', '\u0001', '\u001F', '\u007F', '\u0085', '\u2028', '\u2029',
    };

    [Theory]
    [MemberData(nameof(ForbiddenTitleCharacters))]
    public void Title_rejects_control_and_line_separator_characters_inside_the_text(char forbidden) =>
        Assert.Throws<ArgumentException>(() => Timed(title: $"a{forbidden}b"));

    [Theory]
    [MemberData(nameof(ForbiddenTitleCharacters))]
    public void Replace_rejects_the_same_title_characters_and_leaves_the_entry_untouched(char forbidden)
    {
        var entry = Timed();
        var updatedAt = entry.UpdatedAt;

        Assert.Throws<ArgumentException>(() =>
            entry.Replace($"a{forbidden}b", null, "#654321", false, Start, null, null, null, null));

        Assert.Equal("Call", entry.Title);
        Assert.Equal(1, entry.RowVersion);
        Assert.Equal(updatedAt, entry.UpdatedAt);
    }

    [Theory]
    [InlineData('\0')]
    [InlineData('\u0001')]
    [InlineData('\u000B')]
    [InlineData('\f')]
    [InlineData('\u001F')]
    public void Notes_reject_control_characters_other_than_tab_and_line_breaks(char forbidden)
    {
        Assert.Throws<ArgumentException>(() => Timed(notes: $"line{forbidden}line"));
        Assert.Throws<ArgumentException>(() =>
            Timed().Replace("Call", $"line{forbidden}line", "#123456", false, Start, null, null, null, null));
    }

    [Fact]
    public void Notes_keep_tabs_and_line_breaks_and_unicode_text()
    {
        var notes = "Agenda:\r\n\t1. Fiyat listesi\n\t2. Teslimat — İstanbul 🚚\u2028end";

        Assert.Equal(notes, Timed(notes: notes).Notes);
    }

    [Fact]
    public void Instants_are_truncated_to_the_microsecond_not_rounded()
    {
        var entry = Timed(start: Start.AddTicks(9), end: Start.AddHours(1).AddTicks(19));

        Assert.Equal(Start, entry.StartAt);
        Assert.Equal(Start.AddHours(1).AddTicks(10), entry.EndAt);
        Assert.Equal(0, entry.StartAt!.Value.UtcTicks % 10);
        Assert.Equal(0, entry.EndAt!.Value.UtcTicks % 10);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 9)]
    [InlineData(3, 8)]
    public void An_end_less_than_a_microsecond_after_the_start_is_rejected_because_the_database_would_see_them_equal(long startTicks, long endTicks) =>
        Assert.Throws<ArgumentException>(() => Timed(start: Start.AddTicks(startTicks), end: Start.AddTicks(endTicks)));

    [Fact]
    public void An_end_one_microsecond_after_the_start_is_accepted() =>
        Assert.Equal(Start.AddTicks(10), Timed(start: Start, end: Start.AddTicks(10)).EndAt);

    [Fact]
    public void Replace_truncates_and_validates_instants_the_same_way()
    {
        var entry = Timed();

        Assert.Throws<ArgumentException>(() =>
            entry.Replace("Call", null, "#123456", false, Start, Start.AddTicks(5), null, null, null));
        entry.Replace("Call", null, "#123456", false, Start.AddTicks(7), Start.AddTicks(17), null, null, null);

        Assert.Equal(Start, entry.StartAt);
        Assert.Equal(Start.AddTicks(10), entry.EndAt);
        Assert.Equal(2, entry.RowVersion);
    }
}
