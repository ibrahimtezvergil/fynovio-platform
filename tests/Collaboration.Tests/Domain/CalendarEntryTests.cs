using Collaboration.Domain;
using Contracts;

namespace Collaboration.Tests.Domain;

public sealed class CalendarEntryCreationTests
{
    private static readonly TenantId Tenant = new(1);
    private static readonly PrincipalRef Owner = new("test-issuer", "test-subject");
    private static readonly DateTimeOffset UtcNow = DateTimeOffset.UtcNow;

    [Fact]
    public void Create_sets_row_version_to_1()
    {
        var entry = CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, UtcNow, null, null, null, null);
        Assert.Equal(1, entry.RowVersion);
    }

    [Fact]
    public void Create_accepts_valid_timed_entry()
    {
        var start = new DateTimeOffset(2026, 9, 21, 9, 0, 0, TimeSpan.FromHours(3));
        var end = new DateTimeOffset(2026, 9, 21, 10, 0, 0, TimeSpan.FromHours(3));
        var entry = CalendarEntry.Create(Tenant, Owner, "Call", "Notes", "#123456", false, start, end, null, null, null);

        Assert.NotNull(entry);
        Assert.False(entry.AllDay);
        Assert.Equal(start.ToUniversalTime(), entry.StartAt);
        Assert.Equal(end.ToUniversalTime(), entry.EndAt);
    }

    [Fact]
    public void Create_accepts_valid_all_day_entry()
    {
        var start = new DateOnly(2026, 9, 21);
        var end = new DateOnly(2026, 9, 22);
        var entry = CalendarEntry.Create(Tenant, Owner, "Event", null, "#aabbcc", true, null, null, start, end, null);

        Assert.NotNull(entry);
        Assert.True(entry.AllDay);
        Assert.Equal(start, entry.StartDate);
        Assert.Equal(end, entry.EndDate);
    }

    // PrincipalRef's constructor already rejects blanks; only default(PrincipalRef) can reach the aggregate with null parts.
    [Fact]
    public void Create_rejects_default_owner() =>
        Assert.Throws<ArgumentException>(() =>
            CalendarEntry.Create(Tenant, default, "Title", null, "#123456", false, UtcNow, null, null, null, null));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("  ")]
    public void Create_rejects_blank_title(string title)
    {
        Assert.Throws<ArgumentException>(() =>
            CalendarEntry.Create(Tenant, Owner, title, null, "#123456", false, UtcNow, null, null, null, null));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("Title with spaces")]
    [InlineData("A")]
    public void Create_accepts_valid_title_length_and_case(string title)
    {
        var entry = CalendarEntry.Create(Tenant, Owner, title, null, "#123456", false, UtcNow, null, null, null, null);
        Assert.Equal(title, entry.Title);
    }

    [Fact]
    public void Create_accepts_title_exactly_200_chars()
    {
        var title = new string('a', 200);
        var entry = CalendarEntry.Create(Tenant, Owner, title, null, "#123456", false, UtcNow, null, null, null, null);
        Assert.Equal(200, entry.Title.Length);
    }

    [Fact]
    public void Create_rejects_title_201_chars()
    {
        var title = new string('a', 201);
        Assert.Throws<ArgumentException>(() =>
            CalendarEntry.Create(Tenant, Owner, title, null, "#123456", false, UtcNow, null, null, null, null));
    }

    [Theory]
    [InlineData(" title")]
    [InlineData("title ")]
    [InlineData(" title ")]
    public void Create_rejects_untrimmed_title(string title)
    {
        Assert.Throws<ArgumentException>(() =>
            CalendarEntry.Create(Tenant, Owner, title, null, "#123456", false, UtcNow, null, null, null, null));
    }

    [Theory]
    [InlineData("title\nsubtitle")]
    [InlineData("title\rsubtitle")]
    public void Create_rejects_multiline_title(string title)
    {
        Assert.Throws<ArgumentException>(() =>
            CalendarEntry.Create(Tenant, Owner, title, null, "#123456", false, UtcNow, null, null, null, null));
    }

    [Fact]
    public void Create_accepts_null_notes()
    {
        var entry = CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, UtcNow, null, null, null, null);
        Assert.Null(entry.Notes);
    }

    [Fact]
    public void Create_accepts_notes_4000_chars()
    {
        var notes = new string('x', 4000);
        var entry = CalendarEntry.Create(Tenant, Owner, "Title", notes, "#123456", false, UtcNow, null, null, null, null);
        Assert.Equal(4000, entry.Notes!.Length);
    }

    [Fact]
    public void Create_rejects_notes_4001_chars()
    {
        var notes = new string('x', 4001);
        Assert.Throws<ArgumentException>(() =>
            CalendarEntry.Create(Tenant, Owner, "Title", notes, "#123456", false, UtcNow, null, null, null, null));
    }

    [Fact]
    public void Create_normalizes_color_to_lowercase()
    {
        var entry = CalendarEntry.Create(Tenant, Owner, "Title", null, "#AABBCC", false, UtcNow, null, null, null, null);
        Assert.Equal("#aabbcc", entry.Color);
    }

    [Theory]
    [InlineData("#AABBCC")]
    [InlineData("#AbCdEf")]
    public void Create_accepts_uppercase_hex_color(string color)
    {
        var entry = CalendarEntry.Create(Tenant, Owner, "Title", null, color, false, UtcNow, null, null, null, null);
        Assert.Equal(color.ToLowerInvariant(), entry.Color);
    }

    [Theory]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("123456")]
    [InlineData("#GGGGGG")]
    [InlineData("")]
    [InlineData("#aabbcc\n")]
    [InlineData("#aabbcc\r\n")]
    [InlineData("\n#aabbcc")]
    [InlineData(" #aabbcc")]
    public void Create_rejects_invalid_color_format(string color)
    {
        Assert.Throws<ArgumentException>(() =>
            CalendarEntry.Create(Tenant, Owner, "Title", null, color, false, UtcNow, null, null, null, null));
    }

    [Fact]
    public void Create_normalizes_timestamps_to_utc()
    {
        var startWithOffset = new DateTimeOffset(2026, 9, 21, 9, 0, 0, TimeSpan.FromHours(3));
        var endWithOffset = new DateTimeOffset(2026, 9, 21, 10, 0, 0, TimeSpan.FromHours(-5));

        var entry = CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, startWithOffset, endWithOffset, null, null, null);

        Assert.Equal(TimeSpan.Zero, entry.StartAt!.Value.Offset);
        Assert.Equal(TimeSpan.Zero, entry.EndAt!.Value.Offset);
        Assert.Equal(startWithOffset.ToUniversalTime().UtcDateTime, entry.StartAt!.Value.UtcDateTime);
        Assert.Equal(endWithOffset.ToUniversalTime().UtcDateTime, entry.EndAt!.Value.UtcDateTime);
    }

    [Fact]
    public void Create_all_day_rejects_start_at()
    {
        Assert.Throws<ArgumentException>(() =>
            CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", true, UtcNow, null, new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 22), null));
    }

    [Fact]
    public void Create_all_day_rejects_end_at()
    {
        Assert.Throws<ArgumentException>(() =>
            CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", true, null, UtcNow, new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 22), null));
    }

    [Fact]
    public void Create_all_day_requires_start_date()
    {
        Assert.Throws<ArgumentException>(() =>
            CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", true, null, null, null, new DateOnly(2026, 9, 22), null));
    }

    [Fact]
    public void Create_all_day_requires_end_date()
    {
        Assert.Throws<ArgumentException>(() =>
            CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", true, null, null, new DateOnly(2026, 9, 21), null, null));
    }

    [Fact]
    public void Create_all_day_requires_end_date_greater_than_start_date()
    {
        var start = new DateOnly(2026, 9, 21);
        Assert.Throws<ArgumentException>(() =>
            CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", true, null, null, start, start, null));
    }

    [Fact]
    public void Create_all_day_accepts_end_date_equal_to_start_plus_one()
    {
        var start = new DateOnly(2026, 9, 21);
        var end = new DateOnly(2026, 9, 22);
        var entry = CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", true, null, null, start, end, null);
        Assert.Equal(start, entry.StartDate);
        Assert.Equal(end, entry.EndDate);
    }

    [Fact]
    public void Create_timed_rejects_start_date()
    {
        Assert.Throws<ArgumentException>(() =>
            CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, UtcNow, null, new DateOnly(2026, 9, 21), null, null));
    }

    [Fact]
    public void Create_timed_rejects_end_date()
    {
        Assert.Throws<ArgumentException>(() =>
            CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, UtcNow, null, null, new DateOnly(2026, 9, 22), null));
    }

    [Fact]
    public void Create_timed_requires_start_at()
    {
        Assert.Throws<ArgumentException>(() =>
            CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, null, null, null, null, null));
    }

    [Fact]
    public void Create_timed_accepts_null_end_at()
    {
        var entry = CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, UtcNow, null, null, null, null);
        Assert.Null(entry.EndAt);
    }

    [Fact]
    public void Create_timed_requires_end_at_greater_than_start_at()
    {
        var start = UtcNow;
        Assert.Throws<ArgumentException>(() =>
            CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, start, start, null, null, null));
    }

    [Fact]
    public void Create_timed_accepts_end_at_equal_to_start_plus_one()
    {
        var start = UtcNow;
        var end = start.AddHours(1);
        var entry = CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, start, end, null, null, null);
        Assert.Equal(start.ToUniversalTime(), entry.StartAt);
        Assert.Equal(end.ToUniversalTime(), entry.EndAt);
    }

    [Fact]
    public void Create_rejects_link_with_mismatched_tenant()
    {
        var link = new EntityRef(new TenantId(2), "crm", "opportunity", 123);
        Assert.Throws<ArgumentException>(() =>
            CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, UtcNow, null, null, null, link));
    }

    [Fact]
    public void Create_accepts_link_with_matching_tenant()
    {
        var link = new EntityRef(Tenant, "crm", "opportunity", 123);
        var entry = CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, UtcNow, null, null, null, link);
        Assert.Equal("crm", entry.LinkBoundedContext);
        Assert.Equal("opportunity", entry.LinkEntityType);
        Assert.Equal(123, entry.LinkEntityId);
    }

    [Fact]
    public void Create_accepts_null_link()
    {
        var entry = CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, UtcNow, null, null, null, null);
        Assert.Null(entry.Link);
    }
}

public sealed class CalendarEntryReplaceTests
{
    private static readonly TenantId Tenant = new(1);
    private static readonly PrincipalRef Owner = new("test-issuer", "test-subject");
    private static readonly DateTimeOffset UtcNow = DateTimeOffset.UtcNow;

    [Fact]
    public void Replace_increments_row_version_exactly_once()
    {
        var entry = CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, UtcNow, null, null, null, null);
        var originalVersion = entry.RowVersion;

        entry.Replace("NewTitle", null, "#654321", false, UtcNow, null, null, null, null);

        Assert.Equal(originalVersion + 1, entry.RowVersion);
    }

    [Fact]
    public void Replace_updates_title()
    {
        var entry = CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, UtcNow, null, null, null, null);
        entry.Replace("NewTitle", null, "#654321", false, UtcNow, null, null, null, null);
        Assert.Equal("NewTitle", entry.Title);
    }

    [Fact]
    public void Replace_updates_notes()
    {
        var entry = CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, UtcNow, null, null, null, null);
        entry.Replace("Title", "NewNotes", "#654321", false, UtcNow, null, null, null, null);
        Assert.Equal("NewNotes", entry.Notes);
    }

    [Fact]
    public void Replace_normalizes_color_on_update()
    {
        var entry = CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, UtcNow, null, null, null, null);
        entry.Replace("Title", null, "#AABBCC", false, UtcNow, null, null, null, null);
        Assert.Equal("#aabbcc", entry.Color);
    }

    [Fact]
    public void Replace_normalizes_timestamps_to_utc_on_update()
    {
        var entry = CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, UtcNow, null, null, null, null);
        var startWithOffset = new DateTimeOffset(2026, 9, 21, 9, 0, 0, TimeSpan.FromHours(3));
        var endWithOffset = new DateTimeOffset(2026, 9, 21, 10, 0, 0, TimeSpan.FromHours(-5));

        entry.Replace("Title", null, "#123456", false, startWithOffset, endWithOffset, null, null, null);

        Assert.Equal(TimeSpan.Zero, entry.StartAt!.Value.Offset);
        Assert.Equal(TimeSpan.Zero, entry.EndAt!.Value.Offset);
    }

    [Fact]
    public void Replace_is_atomic_on_invalid_title()
    {
        var entry = CalendarEntry.Create(Tenant, Owner, "OriginalTitle", "OriginalNotes", "#AABBCC", false, UtcNow, null, null, null, null);
        var originalVersion = entry.RowVersion;
        var originalUpdatedAt = entry.UpdatedAt;

        Assert.Throws<ArgumentException>(() =>
            entry.Replace(" InvalidTitle", "NewNotes", "#654321", false, UtcNow, null, null, null, null));

        Assert.Equal("OriginalTitle", entry.Title);
        Assert.Equal("OriginalNotes", entry.Notes);
        Assert.Equal("#aabbcc", entry.Color);
        Assert.Equal(originalVersion, entry.RowVersion);
        Assert.Equal(originalUpdatedAt, entry.UpdatedAt);
    }

    [Fact]
    public void Replace_is_atomic_on_invalid_color()
    {
        var entry = CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, UtcNow, null, null, null, null);
        var originalVersion = entry.RowVersion;
        var originalTitle = entry.Title;

        Assert.Throws<ArgumentException>(() =>
            entry.Replace("NewTitle", null, "#GGGGGG", false, UtcNow, null, null, null, null));

        Assert.Equal(originalTitle, entry.Title);
        Assert.Equal(originalVersion, entry.RowVersion);
    }

    [Fact]
    public void Replace_is_atomic_on_invalid_timing()
    {
        var entry = CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, UtcNow, null, null, null, null);
        var originalVersion = entry.RowVersion;
        var originalStartAt = entry.StartAt;

        Assert.Throws<ArgumentException>(() =>
            entry.Replace("Title", null, "#123456", false, UtcNow, UtcNow, null, null, null));

        Assert.Equal(originalStartAt, entry.StartAt);
        Assert.Equal(originalVersion, entry.RowVersion);
    }

    [Fact]
    public void Replace_rejects_link_with_mismatched_tenant()
    {
        var entry = CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, UtcNow, null, null, null, null);
        var mismatchedLink = new EntityRef(new TenantId(2), "crm", "opportunity", 123);

        Assert.Throws<ArgumentException>(() =>
            entry.Replace("Title", null, "#123456", false, UtcNow, null, null, null, mismatchedLink));
    }

    [Fact]
    public void Replace_accepts_link_with_matching_tenant()
    {
        var entry = CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, UtcNow, null, null, null, null);
        var link = new EntityRef(Tenant, "crm", "opportunity", 123);

        entry.Replace("Title", null, "#123456", false, UtcNow, null, null, null, link);

        Assert.Equal("crm", entry.LinkBoundedContext);
        Assert.Equal("opportunity", entry.LinkEntityType);
        Assert.Equal(123, entry.LinkEntityId);
    }
}

public sealed class CalendarEntryImmutabilityTests
{
    private static readonly TenantId Tenant = new(1);
    private static readonly PrincipalRef Owner = new("test-issuer", "test-subject");
    private static readonly DateTimeOffset UtcNow = DateTimeOffset.UtcNow;

    [Fact]
    public void Owner_issuer_is_immutable()
    {
        var entry = CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, UtcNow, null, null, null, null);
        var originalOwner = entry.Owner;
        entry.Replace("NewTitle", null, "#654321", false, UtcNow, null, null, null, null);
        Assert.Equal(originalOwner, entry.Owner);
    }

    [Fact]
    public void Owner_subject_is_immutable()
    {
        var entry = CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, UtcNow, null, null, null, null);
        var originalSubject = entry.OwnerPrincipalSubject;
        entry.Replace("NewTitle", null, "#654321", false, UtcNow, null, null, null, null);
        Assert.Equal(originalSubject, entry.OwnerPrincipalSubject);
    }

    [Fact]
    public void Tenant_is_immutable()
    {
        var entry = CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, UtcNow, null, null, null, null);
        var originalTenant = entry.TenantId;
        entry.Replace("NewTitle", null, "#654321", false, UtcNow, null, null, null, null);
        Assert.Equal(originalTenant, entry.TenantId);
    }

    [Fact]
    public void CreatedAt_is_immutable()
    {
        var entry = CalendarEntry.Create(Tenant, Owner, "Title", null, "#123456", false, UtcNow, null, null, null, null);
        var originalCreatedAt = entry.CreatedAt;
        entry.Replace("NewTitle", null, "#654321", false, UtcNow, null, null, null, null);
        Assert.Equal(originalCreatedAt, entry.CreatedAt);
    }
}
