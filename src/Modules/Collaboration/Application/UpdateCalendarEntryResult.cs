namespace Collaboration.Application;

public sealed record UpdateCalendarEntryResult(long Id, long RowVersion, bool Replayed);
