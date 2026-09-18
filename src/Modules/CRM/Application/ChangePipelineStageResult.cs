namespace CRM.Application;

public sealed record ChangePipelineStageResult(long OpportunityId, long PipelineStageId, bool Replayed);
