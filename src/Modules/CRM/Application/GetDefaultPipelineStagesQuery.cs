using Contracts;

namespace CRM.Application;

public sealed record GetDefaultPipelineStagesQuery(TenantId TenantId, PrincipalRef Principal, Guid CorrelationId);
