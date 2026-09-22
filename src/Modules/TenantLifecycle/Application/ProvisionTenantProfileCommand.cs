using Contracts;

namespace TenantLifecycle.Application;

/// <summary>System lifecycle command. Host invokes it after Access bootstrap; it is idempotent by profile state.</summary>
public sealed record ProvisionTenantProfileCommand(TenantId TenantId, string DisplayName, Guid CorrelationId);
