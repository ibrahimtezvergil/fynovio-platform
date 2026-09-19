using Contracts;

namespace CRM.Application;

public sealed record GetOpportunityAvailableActionsQuery(TenantId TenantId, long OpportunityId, PrincipalRef Principal, Guid CorrelationId);
