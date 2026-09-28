using Contracts;

namespace CRM.Application;

public sealed record MoveOpportunityToPipelineCommand(
    TenantId TenantId,
    long OpportunityId,
    PrincipalRef Principal,
    long ExpectedVersion,
    long TargetPipelineDefinitionVersionId,
    long TargetStageId,
    string IdempotencyKey,
    Guid CorrelationId);
