namespace Collaboration.Application;

public sealed record CreateCalendarEntryResult(long Id, long RowVersion, bool Replayed);
