using System.Text.RegularExpressions;
using Contracts;

namespace Collaboration.Domain;

public sealed partial class CalendarEntry : IHasRowVersion
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
        var entry = new CalendarEntry { TenantId = tenantId, OwnerPrincipalIssuer = owner.Issuer, OwnerPrincipalSubject = owner.Subject };
        entry.Apply(title, notes, color, allDay, startAt, endAt, startDate, endDate, link);
        var now = DateTimeOffset.UtcNow;
        entry.CreatedAt = now;
        entry.UpdatedAt = now;
        return entry;
    }

    public void Replace(
        string title, string? notes, string color, bool allDay, DateTimeOffset? startAt,
        DateTimeOffset? endAt, DateOnly? startDate, DateOnly? endDate, EntityRef? link)
    {
        Apply(title, notes, color, allDay, startAt, endAt, startDate, endDate, link);
        UpdatedAt = DateTimeOffset.UtcNow;
        RowVersion++;
    }

    private void Apply(
        string title, string? notes, string color, bool allDay, DateTimeOffset? startAt,
        DateTimeOffset? endAt, DateOnly? startDate, DateOnly? endDate, EntityRef? link)
    {
        if (link is { } value && value.TenantId != TenantId)
            throw new ArgumentException("Link tenant must match the calendar entry tenant.", nameof(link));
        Title = NormalizeTitle(title);
        Notes = ValidateNotes(notes);
        Color = NormalizeColor(color);
        ValidateTiming(allDay, startAt, endAt, startDate, endDate);
        AllDay = allDay;
        StartAt = startAt;
        EndAt = endAt;
        StartDate = startDate;
        EndDate = endDate;
        LinkBoundedContext = link?.BoundedContext;
        LinkEntityType = link?.EntityType;
        LinkEntityId = link?.Id;
    }

    private static string NormalizeTitle(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        if (title != title.Trim() || title.Length is < 1 or > 200 || title.Contains('\n') || title.Contains('\r'))
            throw new ArgumentException("Title must be a trimmed single line of 1 to 200 characters.", nameof(title));
        return title;
    }

    private static string? ValidateNotes(string? notes)
    {
        if (notes?.Length > 4000)
            throw new ArgumentException("Notes cannot exceed 4000 characters.", nameof(notes));
        return notes;
    }

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

    void IHasRowVersion.IncrementRowVersion() => RowVersion++;

    [GeneratedRegex("^#[0-9a-f]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex ColorPattern();
}
