using Collaboration.Domain;
using Contracts;

namespace Collaboration.Application;

/// <summary>The two places an entry's link meets another module: write-time validation and read-time hydration. Both go
/// through <see cref="ILinkTargetDirectory"/> (which asks whichever module owns the target, under the actor's own
/// authorization); this module never learns who that is, and never stores what comes back.</summary>
internal static class CalendarLinks
{
    /// <summary>Throws <see cref="CalendarLinkTargetUnavailableException"/> unless the actor may see the target right now.
    /// Unknown type, missing, other tenant, denied and a directory that fails are one and the same outcome.</summary>
    public static async Task EnsureResolvableAsync(
        ILinkTargetDirectory directory, ActorContext actor, EntityRef link, CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(directory, actor, [link], cancellationToken);
        if (!resolved.TryGetValue(link, out var target) || target is not LinkTargetResolution.Accessible)
            throw new CalendarLinkTargetUnavailableException();
    }

    /// <summary>The entries as DTOs, their links hydrated with ONE batched directory call (none when nothing is linked).
    /// A link that cannot be resolved — for any reason, including the directory failing — reads as unavailable; it never
    /// fails the read.</summary>
    public static async Task<IReadOnlyList<CalendarEntryDto>> ToDtosAsync(
        ILinkTargetDirectory directory, ActorContext actor, IReadOnlyList<CalendarEntry> entries, CancellationToken cancellationToken)
    {
        var references = entries.Select(e => e.Link).OfType<EntityRef>().Distinct().ToList();

        IReadOnlyDictionary<EntityRef, LinkTargetResolution> resolved = new Dictionary<EntityRef, LinkTargetResolution>();
        if (references.Count > 0)
        {
            try
            {
                resolved = await ResolveAsync(directory, actor, references, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Degrades below to "unavailable"; the exception is dropped on purpose (it may carry target data).
            }
        }

        return entries.Select(entry => ToDto(entry, resolved)).ToList();
    }

    private static async Task<IReadOnlyDictionary<EntityRef, LinkTargetResolution>> ResolveAsync(
        ILinkTargetDirectory directory, ActorContext actor, IReadOnlyCollection<EntityRef> references, CancellationToken cancellationToken)
    {
        try
        {
            return await directory.ResolveAsync(actor, references, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new CalendarLinkTargetUnavailableException();
        }
    }

    private static CalendarEntryDto ToDto(CalendarEntry entry, IReadOnlyDictionary<EntityRef, LinkTargetResolution> resolved)
    {
        CalendarEntryLinkDto? link = null;
        if (entry.Link is { } reference)
        {
            link = resolved.TryGetValue(reference, out var target) && target is LinkTargetResolution.Accessible accessible
                ? new CalendarEntryLinkDto(reference, Accessible: true, accessible.Label, accessible.Subtitle)
                : new CalendarEntryLinkDto(reference, Accessible: false);
        }

        return new CalendarEntryDto(
            entry.Id, entry.RowVersion, entry.Title, entry.Notes, entry.Color, entry.AllDay,
            entry.StartAt, entry.EndAt, entry.StartDate, entry.EndDate, link);
    }
}
