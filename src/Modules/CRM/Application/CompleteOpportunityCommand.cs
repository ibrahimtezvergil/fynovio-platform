using Contracts;

namespace CRM.Application;

/// <summary>The module's first public command (doc 08: a module's outside surface is
/// commands, queries and published events). The caller generates <see cref="IdempotencyKey"/>;
/// <see cref="CorrelationId"/> ties the call chain together (doc 04).</summary>
public sealed record CompleteOpportunityCommand(
    TenantId TenantId,
    long OpportunityId,
    PrincipalRef Principal,
    string IdempotencyKey,
    Guid CorrelationId);
