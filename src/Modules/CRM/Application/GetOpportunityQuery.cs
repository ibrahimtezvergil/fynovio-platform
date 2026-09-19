using Contracts;

namespace CRM.Application;

public sealed record GetOpportunityQuery(TenantId TenantId, long OpportunityId, PrincipalRef Principal, Guid CorrelationId);
