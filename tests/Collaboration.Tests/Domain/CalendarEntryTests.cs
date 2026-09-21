using Collaboration.Domain;
using Contracts;

namespace Collaboration.Tests.Domain;

public sealed class CalendarEntryTests
{
    private static readonly TenantId Tenant = new(1);
    private static readonly PrincipalRef Owner = new("test", "owner");

    [Fact]
    public void Create_normalizes_color_and_keeps_valid_timed_entry()
    {
        var entry = CalendarEntry.Create(Tenant, Owner, "Call", null, "#AABBCC", false, DateTimeOffset.Parse("2026-09-21T09:00:00+03:00"), DateTimeOffset.Parse("2026-09-21T10:00:00+03:00"), null, null, null);
        Assert.Equal("#aabbcc", entry.Color);
        Assert.Equal(1, entry.RowVersion);
    }

    [Theory]
    [InlineData(" title")]
    [InlineData("title ")]
    [InlineData("title\nnext")]
    public void Create_rejects_non_trimmed_or_multiline_title(string title) =>
        Assert.Throws<ArgumentException>(() => CalendarEntry.Create(Tenant, Owner, title, null, "#123456", false, DateTimeOffset.UtcNow, null, null, null, null));

    [Fact]
    public void All_day_requires_exclusive_end_date() =>
        Assert.Throws<ArgumentException>(() => CalendarEntry.Create(Tenant, Owner, "Day", null, "#123456", true, null, null, new DateOnly(2026, 9, 21), null, null));

    [Fact]
    public void Replace_increments_version() 
    {
        var entry = CalendarEntry.Create(Tenant, Owner, "Call", null, "#123456", false, DateTimeOffset.UtcNow, null, null, null, null);
        entry.Replace("Updated", null, "#654321", false, DateTimeOffset.UtcNow, null, null, null, null);
        Assert.Equal(2, entry.RowVersion);
    }
}
