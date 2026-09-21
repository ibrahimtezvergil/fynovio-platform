using Contracts;

namespace Collaboration.Application;

public sealed record GetCalendarEntryQuery(TenantId TenantId, long Id, PrincipalRef Principal, Guid CorrelationId);
