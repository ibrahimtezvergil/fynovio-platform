using Contracts;
using TenantLifecycle.Domain;

namespace TenantLifecycle.Application;

public sealed record UpdateCompanySettingsCommand(
    TenantId TenantId,
    PrincipalRef Principal,
    TenantProfileDetails Details,
    long ExpectedVersion,
    string IdempotencyKey,
    Guid CorrelationId);
