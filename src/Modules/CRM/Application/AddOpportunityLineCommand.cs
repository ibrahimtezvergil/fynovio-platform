using Contracts;

namespace CRM.Application;

public sealed record AddOpportunityLineCommand(
    TenantId TenantId,
    long OpportunityId,
    PrincipalRef Principal,
    long ExpectedVersion,
    EntityRef ProductRef,
    int Quantity,
    decimal UnitPrice,
    bool IsOptional,
    int SortOrder,
    string IdempotencyKey,
    Guid CorrelationId);
