using Contracts;

namespace Access.Application;

/// <summary>NOT exposed over HTTP. Called by the operator command and the Development seed. `Actor` is the
/// principal recorded in evidence — an operator identity, since enablement is a platform act.</summary>
public sealed record EnableTenantModuleCommand(TenantId TenantId, string ModuleKey, PrincipalRef Actor, Guid CorrelationId);

public enum EnableTenantModuleStatus
{
    Enabled,

    /// <summary>The tenant already has this module. Nothing was changed — not even to a newer template version.</summary>
    AlreadyEnabled,

    UnknownModule,
    TenantNotBootstrapped,

    /// <summary>The tenant already owns a role or permission set with a key/name the template would create.
    /// Never adopted or overwritten.</summary>
    TemplateKeyConflict
}

public sealed record EnableTenantModuleResult(
    EnableTenantModuleStatus Status,
    string ModuleKey,
    int? EnabledVersion = null,
    int? LatestVersion = null,
    int GrantedAssignments = 0,
    string? Detail = null);
