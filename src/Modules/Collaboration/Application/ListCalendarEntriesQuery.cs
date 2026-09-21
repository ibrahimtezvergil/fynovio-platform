using Contracts;

namespace Collaboration.Application;

public sealed record ListCalendarEntriesQuery(TenantId TenantId, PrincipalRef Principal, DateTimeOffset From, DateTimeOffset To, Guid CorrelationId);
