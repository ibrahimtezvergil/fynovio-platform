namespace Collaboration.Application;

public sealed record CollaborationActionDescriptor(string ActionKey, string ResourceType, string? RiskClass = null);

public static class CollaborationActionCatalog
{
    public static readonly IReadOnlyList<CollaborationActionDescriptor> All =
    [
        new(CollaborationActionKeys.CalendarEntryCreate, "CalendarEntry"),
        new(CollaborationActionKeys.CalendarEntryRead, "CalendarEntry"),
        new(CollaborationActionKeys.CalendarEntryList, "CalendarEntry"),
        new(CollaborationActionKeys.CalendarEntryUpdate, "CalendarEntry"),
        new(CollaborationActionKeys.CalendarEntryDelete, "CalendarEntry")
    ];
}
