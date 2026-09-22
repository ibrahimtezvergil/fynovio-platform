namespace TenantLifecycle.Application;

public sealed record ProvisionTenantProfileResult(long RowVersion, bool AlreadyProvisioned);
