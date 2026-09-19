namespace CRM.Application;

/// <summary>Thrown by OpenOpportunityHandler when a tenant has a configured pipeline
/// (a PipelineDefinition and PipelineDefinitionVersion exist) but that version has no
/// usable entry stage — either no stage is flagged IsEntry, or the flagged stage has
/// IsActive == false. Distinct from "tenant has no pipeline configured at all," which
/// stays legal (Opportunity.Open already accepts null version/stage) — see
/// docs/architecture-analysis/2026-09-19-crm-phase2-authorization-delta-and-entry-stage-resolution.md,
/// "OPEN DECISION RESOLUTION — Pipeline Entry Stage Semantics".</summary>
public sealed class PipelineConfigurationInvalidException : InvalidOperationException
{
    public PipelineConfigurationInvalidException(long pipelineDefinitionVersionId, string reason)
        : base($"Pipeline definition version {pipelineDefinitionVersionId} has no usable entry stage: {reason}.")
    {
    }
}
