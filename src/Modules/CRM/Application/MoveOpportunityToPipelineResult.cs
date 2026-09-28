namespace CRM.Application;

public sealed record MoveOpportunityToPipelineResult(long OpportunityId, long TargetPipelineDefinitionVersionId, long TargetStageId, bool Replayed);
