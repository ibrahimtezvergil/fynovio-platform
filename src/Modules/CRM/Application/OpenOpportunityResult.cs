namespace CRM.Application;

public sealed record OpenOpportunityResult(long OpportunityId, long? PipelineStageId, bool Replayed);
