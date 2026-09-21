using Contracts;

namespace Collaboration.Application;

public sealed record UpdateCalendarEntryCommand(
    TenantId TenantId, long EntryId, PrincipalRef Principal, long ExpectedVersion, string Title, string? Notes,
    string Color, bool AllDay, DateTimeOffset? StartAt, DateTimeOffset? EndAt, DateOnly? StartDate,
    DateOnly? EndDate, EntityRef? Link, string IdempotencyKey, Guid CorrelationId);
