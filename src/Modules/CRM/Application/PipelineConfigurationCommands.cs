using Contracts;
using CRM.Domain;

namespace CRM.Application;

/// <summary>`Kind` defaults to Open. A Won/Lost input carries only the tenant's label for that system stage:
/// its position, activity and entry flag are the system's, whatever the client sends.</summary>
public sealed record PipelineStageInput(string Name, int SortOrder, bool IsEntry, bool IsActive, bool IsArchived = false, PipelineStageKind Kind = PipelineStageKind.Open);
public sealed record PipelineTransitionInput(string FromStageName, string ToStageName);
public sealed record CreatePipelineDraftCommand(TenantId TenantId, PrincipalRef Principal, long? PipelineDefinitionId, string Name,
    long ExpectedRowVersion, int ExpectedLatestVersionNumber, IReadOnlyList<PipelineStageInput> Stages,
    bool EnforceAllowedTransitions, IReadOnlyList<PipelineTransitionInput> AllowedTransitions, string IdempotencyKey, Guid CorrelationId);
public sealed record CreatePipelineDraftResult(long PipelineDefinitionId, long VersionId, int VersionNumber, long RowVersion, bool Replayed);
public sealed record PublishPipelineVersionCommand(TenantId TenantId, PrincipalRef Principal, long PipelineDefinitionId, long VersionId,
    long ExpectedPipelineRowVersion, string IdempotencyKey, Guid CorrelationId);
public sealed record PublishPipelineVersionResult(long PipelineDefinitionId, long VersionId, int VersionNumber,
    int OpportunitiesRetainedOnPriorVersions, bool Replayed);
public sealed record PipelineImpactValidationDto(bool IsValid, long? DraftVersionId, int ActiveStageCount, int EntryStageCount,
    int InvalidTransitionCount, int OpportunitiesRetainedOnPriorVersions, IReadOnlyList<string> Errors);
