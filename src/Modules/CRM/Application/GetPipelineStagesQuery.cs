using Contracts;

namespace CRM.Application;

public sealed record GetPipelineStagesQuery(TenantId TenantId, long PipelineDefinitionVersionId, Guid CorrelationId);
