using Contracts;

namespace Collaboration.Application;

public sealed record CreateCalendarEntryCommand(
    TenantId TenantId, PrincipalRef Principal, string Title, string? Notes, string Color,
    bool AllDay, DateTimeOffset? StartAt, DateTimeOffset? EndAt, DateOnly? StartDate,
    DateOnly? EndDate, EntityRef? Link, string IdempotencyKey, Guid CorrelationId);
