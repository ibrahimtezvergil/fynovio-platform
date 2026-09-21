using Contracts;

namespace Collaboration.Application;

/// <summary>A stored link as one actor may see it right now. `Label`/`Subtitle` exist only when `Accessible`; they were computed
/// for that actor at read time and are never persisted.</summary>
public sealed record CalendarEntryLinkDto(EntityRef Ref, bool Accessible, string? Label = null, string? Subtitle = null);
