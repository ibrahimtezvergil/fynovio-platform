using Contracts;

namespace CRM.Application;

public sealed record CreateOpportunityCommand(
    TenantId TenantId,
    PartyRef PartyRef,
    PrincipalRef AssignedPrincipal,
    string Currency,
    decimal EstimatedAmount,
    string IdempotencyKey,
    Guid CorrelationId)
{
    /// <summary>The authenticated caller is distinct from the selected assignee once a tenant default is applied.</summary>
    public PrincipalRef? CallerPrincipal { get; init; }
}
