using Contracts;

namespace CRM.Application;

public sealed record ChangePipelineStageCommand(
    TenantId TenantId,
    long OpportunityId,
    PrincipalRef Principal,
    long ExpectedVersion,
    long TargetStageId,
    string IdempotencyKey,
    Guid CorrelationId);
