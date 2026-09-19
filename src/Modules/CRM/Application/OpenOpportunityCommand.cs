using Contracts;

namespace CRM.Application;

public sealed record OpenOpportunityCommand(
    TenantId TenantId,
    long OpportunityId,
    PrincipalRef Principal,
    long ExpectedVersion,
    DateTimeOffset ExpiryDate,
    string IdempotencyKey,
    Guid CorrelationId);
