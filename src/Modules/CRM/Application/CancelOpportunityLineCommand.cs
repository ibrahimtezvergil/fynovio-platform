using Contracts;

namespace CRM.Application;

public sealed record CancelOpportunityLineCommand(
    TenantId TenantId,
    long OpportunityId,
    long LineId,
    PrincipalRef Principal,
    long ExpectedVersion,
    string CancelReason,
    string IdempotencyKey,
    Guid CorrelationId);
