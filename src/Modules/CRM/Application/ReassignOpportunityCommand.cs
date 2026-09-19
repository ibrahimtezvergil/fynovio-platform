using Contracts;

namespace CRM.Application;

public sealed record ReassignOpportunityCommand(
    TenantId TenantId,
    long OpportunityId,
    PrincipalRef Principal,
    long ExpectedVersion,
    PrincipalRef NewAssignedPrincipal,
    string IdempotencyKey,
    Guid CorrelationId);
