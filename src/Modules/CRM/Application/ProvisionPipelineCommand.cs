using Contracts;

namespace CRM.Application;

/// <summary>`StageNames` are in order; the first is the entry stage. The operator supplies them — the platform does not
/// invent a stage template. `RetiredStageNames` are created already inactive, after the active ones (never the entry
/// stage): a configuration that has a retired stage, which only the Development seed asks for.</summary>
public sealed record ProvisionPipelineCommand(
    TenantId TenantId, string Name, IReadOnlyList<string> StageNames, IReadOnlyList<string>? RetiredStageNames = null);
