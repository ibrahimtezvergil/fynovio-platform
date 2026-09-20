using Contracts;

namespace CRM.Application;

public sealed record ListAssignablePrincipalsQuery(
    TenantId TenantId,
    long OpportunityId,
    PrincipalRef Principal,
    string? Search,
    int Take,
    Guid CorrelationId);

public sealed record AssignablePrincipalDto(string Issuer, string Subject, string DisplayName, string? Email);
