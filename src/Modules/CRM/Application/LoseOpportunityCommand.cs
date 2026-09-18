using Contracts;

namespace CRM.Application;

public sealed record LoseOpportunityCommand(
    TenantId TenantId,
    long OpportunityId,
    PrincipalRef Principal,
    long ExpectedVersion,
    string LostReason,
    string IdempotencyKey,
    Guid CorrelationId);
