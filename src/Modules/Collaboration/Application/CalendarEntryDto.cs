using Contracts;

namespace Collaboration.Application;

public sealed record CalendarEntryDto(long Id, long RowVersion, string Title, string? Notes, string Color, bool AllDay, DateTimeOffset? StartAt, DateTimeOffset? EndAt, DateOnly? StartDate, DateOnly? EndDate, EntityRef? Link);
