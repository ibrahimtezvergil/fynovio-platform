using Contracts;

namespace TenantLifecycle.Application;

public sealed record GetCompanySettingsQuery(TenantId TenantId, PrincipalRef Principal, Guid CorrelationId);
