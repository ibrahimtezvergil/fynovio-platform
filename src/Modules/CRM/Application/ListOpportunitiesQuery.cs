using Contracts;
using CRM.Domain;

namespace CRM.Application;

public sealed record ListOpportunitiesQuery(
    TenantId TenantId,
    PrincipalRef Principal,
    Guid CorrelationId,
    OpportunityStatus? Status,
    int Skip,
    int Take);
