using Contracts;

namespace Access.Application;

/// <summary>NOT exposed over HTTP, ever. Called only from tenant provisioning (once
/// TenantLifecycle's real ProvisionTenant exists) or directly from test fixtures in
/// Phase 1.5. This IS Decision A's "template -> tenant-local instance" mechanism —
/// today's manual invocation, not a reconciler (gap-closure §6).</summary>
public sealed record BootstrapTenantAccessCommand(
    TenantId TenantId,
    PrincipalRef TenantAdministrator,
    Guid CorrelationId);
