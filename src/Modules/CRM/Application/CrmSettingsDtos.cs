using Contracts;
using CRM.Domain;

namespace CRM.Application;

public sealed record CrmSettingsDto(
    long? DefaultPipelineDefinitionId, string OpportunityCreationMode, long? DefaultOpportunityTypeId,
    bool RequireLostReason, bool RequireWonLine, string DefaultAssignmentMode, string AssignmentPolicy,
    PrincipalRef? DefaultPrincipal, long? DefaultTeamId, long? DefaultTerritoryId, long RowVersion,
    IReadOnlyList<string> OpportunityCreationSteps,
    IReadOnlyList<PipelineDefinitionDto> Pipelines,
    IReadOnlyList<CrmConfigurationItemDto> OpportunityTypes, IReadOnlyList<CrmConfigurationItemDto> LostReasons,
    IReadOnlyList<CustomerNeedDto> CustomerNeeds);

public sealed record PipelineDefinitionDto(long Id, string Name, long RowVersion, bool IsActive, bool IsArchived, IReadOnlyList<PipelineVersionDto> Versions);
public sealed record PipelineVersionDto(long Id, int VersionNumber, string Status, bool EnforceAllowedTransitions,
    DateTimeOffset? PublishedAt, IReadOnlyList<PipelineStageDto> Stages, IReadOnlyList<PipelineTransitionDto> AllowedTransitions);
public sealed record PipelineTransitionDto(long FromStageId, long ToStageId);
public sealed record CrmConfigurationItemDto(long Id, string Key, string Name, string Status, long RowVersion);
public sealed record CustomerNeedDto(long Id, string Name, string? Category, decimal AveragePrice, string Status, long RowVersion);

public sealed record UpdateCrmSettingsCommand(
    TenantId TenantId, PrincipalRef Principal, long ExpectedVersion, long? DefaultPipelineDefinitionId,
    OpportunityCreationMode OpportunityCreationMode, long? DefaultOpportunityTypeId, bool RequireLostReason, bool RequireWonLine,
    AssignmentMode DefaultAssignmentMode, AssignmentPolicy AssignmentPolicy, PrincipalRef? DefaultPrincipal,
    long? DefaultTeamId, long? DefaultTerritoryId, string IdempotencyKey, Guid CorrelationId,
    IReadOnlyList<string>? OpportunityCreationSteps = null);

public sealed record UpdateCrmSettingsResult(CrmSettingsDto Settings, bool Replayed);
public sealed record GetCrmSettingsQuery(TenantId TenantId, PrincipalRef Principal, Guid CorrelationId);
