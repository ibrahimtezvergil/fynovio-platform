namespace Collaboration.Application;

/// <summary>An optimistic concurrency conflict on row_version when changing a calendar entry: the caller's expected
/// version did not match the current one, or the row changed between the read and the write.</summary>
public sealed class CalendarEntryConcurrencyConflictException : Exception
{
    public CalendarEntryConcurrencyConflictException(long id, long expectedVersion, long actualVersion)
        : base($"Calendar entry {id} has version {actualVersion}, expected {expectedVersion}.")
    {
    }

    /// <summary>The write lost a race: the row changed after it was read, so the current version is not known here.</summary>
    public CalendarEntryConcurrencyConflictException(long id, long expectedVersion)
        : base($"Calendar entry {id} was changed concurrently; expected version {expectedVersion}.")
    {
    }
}
