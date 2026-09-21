using Contracts;

namespace Collaboration.Application;

public sealed record DeleteCalendarEntryCommand(
    TenantId TenantId, long EntryId, PrincipalRef Principal, long ExpectedVersion, string IdempotencyKey, Guid CorrelationId);
