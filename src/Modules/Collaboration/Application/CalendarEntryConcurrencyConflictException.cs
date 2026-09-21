namespace Collaboration.Application;

/// <summary>An optimistic concurrency conflict on row_version when updating a calendar entry.
/// The caller's expected version did not match the current database version.</summary>
public sealed class CalendarEntryConcurrencyConflictException(long id, long expectedVersion, long actualVersion)
    : Exception($"Calendar entry {id} has version {actualVersion}, expected {expectedVersion}.");
