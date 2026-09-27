namespace CRM.Application;

/// <summary>Thrown by OpenOpportunityHandler when a tenant has CRM enabled but no usable
/// pipeline at all — no PipelineDefinition, or a PipelineDefinition with no Published
/// version. Distinct from PipelineConfigurationInvalidException (a pipeline exists and is
/// published, but its entry stage is missing/inactive). Amends
/// docs/architecture-analysis/2026-09-19-crm-phase2-authorization-delta-and-entry-stage-resolution.md's
/// "tenant has zero PipelineDefinitions → legal" table entry — see
/// docs/architecture-analysis/2026-09-27-crm-opportunity-pipeline-won-lost-stage-integration.md §6.1.</summary>
public sealed class PipelineNotProvisionedException() : InvalidOperationException(
    "This tenant has no usable sales pipeline yet. Provisioning should have happened automatically when CRM was enabled — contact an operator if this persists.");
