using Contracts;

namespace CRM.Application;

public sealed record WinOpportunityCommand(
    TenantId TenantId,
    long OpportunityId,
    PrincipalRef Principal,
    long ExpectedVersion,
    string IdempotencyKey,
    Guid CorrelationId);
