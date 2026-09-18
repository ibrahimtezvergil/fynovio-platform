namespace CRM.Application;

/// <summary>Thrown by ChangePipelineStageHandler when the target stage does not belong
/// to the opportunity's current pipeline version, or is inactive (architecture plan
/// §2.4, §9's ChangePipelineStage row).</summary>
public sealed class InvalidPipelineTransitionException : InvalidOperationException
{
    public InvalidPipelineTransitionException(long opportunityId, long targetStageId, string reason)
        : base($"Cannot move opportunity {opportunityId} to pipeline stage {targetStageId}: {reason}.")
    {
    }
}
