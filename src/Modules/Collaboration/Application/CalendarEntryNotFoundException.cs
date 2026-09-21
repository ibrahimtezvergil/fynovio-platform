namespace Collaboration.Application;

/// <summary>The requested calendar entry does not exist in this tenant.</summary>
public sealed class CalendarEntryNotFoundException(long id)
    : Exception($"Calendar entry {id} not found.");
