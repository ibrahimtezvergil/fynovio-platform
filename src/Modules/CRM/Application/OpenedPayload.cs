namespace CRM.Application;

internal sealed record OpenedPayload(long OpportunityId, long? PipelineDefinitionVersionId, long? PipelineStageId);
