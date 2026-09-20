namespace CRM.Application;

public enum ProvisionPipelineStatus
{
    Provisioned,

    /// <summary>The tenant already has a pipeline. It is left exactly as it is — provisioning is a first-time,
    /// explicit step, not a reconciler.</summary>
    AlreadyProvisioned,
}

public sealed record ProvisionPipelineResult(ProvisionPipelineStatus Status, long? PipelineDefinitionVersionId = null);
