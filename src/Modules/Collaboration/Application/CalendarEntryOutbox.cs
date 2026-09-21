using Collaboration.Domain;

namespace Collaboration.Application;

/// <summary>The thin CloudEvents-shaped facts the calendar commands record. Payloads carry ids, the owner, timing and the
/// link reference only — never the title, the notes or a hydrated target label (schema doc, outbox section).</summary>
internal static class CalendarEntryOutbox
{
    public const string EventSource = "/enterprise/collaboration";
    public const string CreatedEventType = "enterprise.collaboration.calendar-entry.created.v1";
    public const string UpdatedEventType = "enterprise.collaboration.calendar-entry.updated.v1";
    public const string DeletedEventType = "enterprise.collaboration.calendar-entry.deleted.v1";

    public static string Subject(long entryId) => $"calendar-entries/{entryId}";

    public static string EntryPayload(CalendarEntry entry) =>
        System.Text.Json.JsonSerializer.Serialize(new EntryFact(
            entry.Id,
            entry.OwnerPrincipalIssuer,
            entry.OwnerPrincipalSubject,
            entry.AllDay,
            IdempotencySupport.Instant(entry.StartAt),
            IdempotencySupport.Instant(entry.EndAt),
            IdempotencySupport.Date(entry.StartDate),
            IdempotencySupport.Date(entry.EndDate),
            entry.LinkBoundedContext,
            entry.LinkEntityType,
            entry.LinkEntityId));

    /// <summary>The fact that an entry is gone: identity, owner and the version the deletion superseded.</summary>
    public static string DeletedPayload(CalendarEntry entry, long deletedAtVersion) =>
        System.Text.Json.JsonSerializer.Serialize(new DeletedFact(
            entry.Id, entry.OwnerPrincipalIssuer, entry.OwnerPrincipalSubject, deletedAtVersion));

    private sealed record EntryFact(
        long EntryId,
        string OwnerPrincipalIssuer,
        string OwnerPrincipalSubject,
        bool AllDay,
        string? StartAt,
        string? EndAt,
        string? StartDate,
        string? EndDate,
        string? LinkBoundedContext,
        string? LinkEntityType,
        long? LinkEntityId);

    private sealed record DeletedFact(long EntryId, string OwnerPrincipalIssuer, string OwnerPrincipalSubject, long Version);
}
