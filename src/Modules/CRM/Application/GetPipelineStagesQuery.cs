using Contracts;

namespace CRM.Application;

public sealed record GetPipelineStagesQuery(TenantId TenantId, long PipelineDefinitionVersionId, PrincipalRef Principal, Guid CorrelationId);
