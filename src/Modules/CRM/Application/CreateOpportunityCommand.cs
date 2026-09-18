using Contracts;

namespace CRM.Application;

public sealed record CreateOpportunityCommand(
    TenantId TenantId,
    PartyRef PartyRef,
    PrincipalRef AssignedPrincipal,
    string Currency,
    decimal EstimatedAmount,
    string IdempotencyKey,
    Guid CorrelationId);
