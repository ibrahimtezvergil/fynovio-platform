using System.Text.RegularExpressions;
using Contracts;

namespace Collaboration.Domain;

public sealed partial class CalendarEntry
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string OwnerPrincipalIssuer { get; private set; } = null!;
    public string OwnerPrincipalSubject { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string? Notes { get; private set; }
    public string Color { get; private set; } = null!;
    public bool AllDay { get; private set; }
    public DateTimeOffset? StartAt { get; private set; }
    public DateTimeOffset? EndAt { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public string? LinkBoundedContext { get; private set; }
    public string? LinkEntityType { get; private set; }
    public long? LinkEntityId { get; private set; }
    public long RowVersion { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public PrincipalRef Owner => new(OwnerPrincipalIssuer, OwnerPrincipalSubject);
    public EntityRef? Link => LinkEntityId is null ? null : new EntityRef(TenantId, LinkBoundedContext!, LinkEntityType!, LinkEntityId.Value);

    private CalendarEntry() { }

    public static CalendarEntry Create(
        TenantId tenantId, PrincipalRef owner, string title, string? notes, string color,
        bool allDay, DateTimeOffset? startAt, DateTimeOffset? endAt,
        DateOnly? startDate, DateOnly? endDate, EntityRef? link)
    {
        if (string.IsNullOrWhiteSpace(owner.Issuer) || string.IsNullOrWhiteSpace(owner.Subject))
            throw new ArgumentException("Owner principal issuer and subject must be non-blank.", nameof(owner));

        var normalized = NormalizeEntry(title, notes, color, allDay, startAt, endAt, startDate, endDate);
        if (link is { } value && value.TenantId != tenantId)
            throw new ArgumentException("Link tenant must match the calendar entry tenant.", nameof(link));

        var now = DateTimeOffset.UtcNow;
        return new CalendarEntry
        {
            TenantId = tenantId,
            OwnerPrincipalIssuer = owner.Issuer,
            OwnerPrincipalSubject = owner.Subject,
            Title = normalized.Title,
            Notes = normalized.Notes,
            Color = normalized.Color,
            AllDay = allDay,
            StartAt = normalized.StartAt,
            EndAt = normalized.EndAt,
            StartDate = startDate,
            EndDate = endDate,
            LinkBoundedContext = link?.BoundedContext,
            LinkEntityType = link?.EntityType,
            LinkEntityId = link?.Id,
            RowVersion = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Replace(
        string title, string? notes, string color, bool allDay, DateTimeOffset? startAt,
        DateTimeOffset? endAt, DateOnly? startDate, DateOnly? endDate, EntityRef? link)
    {
        var normalized = NormalizeEntry(title, notes, color, allDay, startAt, endAt, startDate, endDate);
        if (link is { } value && value.TenantId != TenantId)
            throw new ArgumentException("Link tenant must match the calendar entry tenant.", nameof(link));

        Title = normalized.Title;
        Notes = normalized.Notes;
        Color = normalized.Color;
        AllDay = allDay;
        StartAt = normalized.StartAt;
        EndAt = normalized.EndAt;
        StartDate = startDate;
        EndDate = endDate;
        LinkBoundedContext = link?.BoundedContext;
        LinkEntityType = link?.EntityType;
        LinkEntityId = link?.Id;
        Touch();
    }

    /// <summary>UTC, at the microsecond precision PostgreSQL stores. Truncating before validation keeps an end that differs from
    /// its start by less than a microsecond from passing here and then colliding with `ck_calendar_entries_end_at`.</summary>
    private static DateTimeOffset? NormalizeTimestamp(DateTimeOffset? dt) => dt is { } value ? TimestampPrecision.Truncate(value) : null;

    private static NormalizedEntry NormalizeEntry(
        string title, string? notes, string color, bool allDay, DateTimeOffset? startAt,
        DateTimeOffset? endAt, DateOnly? startDate, DateOnly? endDate)
    {
        var normalized = new NormalizedEntry(
            NormalizeTitle(title),
            ValidateNotes(notes),
            NormalizeColor(color),
            NormalizeTimestamp(startAt),
            NormalizeTimestamp(endAt));
        ValidateTiming(allDay, normalized.StartAt, normalized.EndAt, startDate, endDate);
        return normalized;
    }

    private void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        RowVersion++;
    }

    private static string NormalizeTitle(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        if (title != title.Trim() || title.Length is < 1 or > 200 || title.Any(IsForbiddenInTitle))
            throw new ArgumentException("Title must be a trimmed single line of 1 to 200 characters without control characters.", nameof(title));
        return title;
    }

    private static string? ValidateNotes(string? notes)
    {
        if (notes?.Length > 4000)
            throw new ArgumentException("Notes cannot exceed 4000 characters.", nameof(notes));
        if (notes is not null && notes.Any(IsForbiddenInNotes))
            throw new ArgumentException("Notes cannot contain control characters other than tab and line breaks.", nameof(notes));
        return notes;
    }

    // A title is a single line: every control character (which covers NUL, tab, vertical tab, form feed, CR, LF, DEL and
    // NEL) and the Unicode line/paragraph separators are out. PostgreSQL text cannot hold NUL at all (22021), so letting one
    // through would surface as a database error instead of a validation error.
    private static bool IsForbiddenInTitle(char c) => char.IsControl(c) || c is '\u2028' or '\u2029';

    // Notes may be multi-line and indented, so tab, LF and CR stay; the rest of the C0 range (NUL included) does not.
    private static bool IsForbiddenInNotes(char c) => c < ' ' && c is not ('\t' or '\n' or '\r');

    private static string NormalizeColor(string color)
    {
        ArgumentNullException.ThrowIfNull(color);
        var normalized = color.ToLowerInvariant();
        if (!ColorPattern().IsMatch(normalized))
            throw new ArgumentException("Color must be a #rrggbb value.", nameof(color));
        return normalized;
    }

    private static void ValidateTiming(bool allDay, DateTimeOffset? startAt, DateTimeOffset? endAt, DateOnly? startDate, DateOnly? endDate)
    {
        if (allDay)
        {
            if (startDate is null || endDate is null || startAt is not null || endAt is not null || endDate <= startDate)
                throw new ArgumentException("All-day entries require dates only and an exclusive end after the start.");
            return;
        }
        if (startAt is null || startDate is not null || endDate is not null || (endAt is not null && endAt <= startAt))
            throw new ArgumentException("Timed entries require a start instant and an exclusive end after it.");
    }

    // \z, not $: in .NET `$` also matches before a final newline, and a value like "#aabbcc\n" would pass here and then
    // fail the column (22001) and ck_calendar_entries_color as an unhandled 500.
    [GeneratedRegex(@"^#[0-9a-f]{6}\z", RegexOptions.CultureInvariant)]
    private static partial Regex ColorPattern();

    private sealed record NormalizedEntry(
        string Title, string? Notes, string Color, DateTimeOffset? StartAt, DateTimeOffset? EndAt);
}
