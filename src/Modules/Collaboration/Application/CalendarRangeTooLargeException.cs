namespace Collaboration.Application;

/// <summary>The requested calendar range is too large — either longer than 100 days
/// or would return more than 500 results.</summary>
public sealed class CalendarRangeTooLargeException(string reason)
    : Exception($"Calendar range too large: {reason}");
