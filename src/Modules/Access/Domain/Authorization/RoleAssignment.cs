using Contracts;

namespace Access.Domain.Authorization;

public enum RoleAssignmentScopeType
{
    Tenant,
    OrganizationUnit,
    Network
}

/// <summary>Grants, separate from membership (doc 08's Access row). `ScopeType`/`ScopeId`
/// resolve through Organization's `ResolveScopeAt` when `OrganizationUnit`, and reference a
/// `tenant-network-schema.md` network id by value (no FK — same `EntityRef`-style convention
/// as <see cref="Contracts.PrincipalRef"/>) when `Network`.
///
/// <b>Intentionally under-validated for now:</b> this enum's exact list and per-value
/// invariants are still open (docs/architecture-analysis/19_IDENTITY_ACCESS_AND_DEALER_NETWORK.md
/// §9, "needs to be finalized once Organization's ResolveScopeAt contract is implemented") —
/// do not add a cross-field `ScopeId` invariant here until that lands, or it will need
/// unwinding.</summary>
public sealed class RoleAssignment : IHasRowVersion
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public long AccountId { get; private set; }
    public long RoleId { get; private set; }
    public RoleAssignmentScopeType ScopeType { get; private set; }
    public long? ScopeId { get; private set; }
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset? ValidTo { get; private set; }
    public long RowVersion { get; private set; } = 1;

    private RoleAssignment() { }

    public static RoleAssignment Grant(
        TenantId tenantId,
        long accountId,
        long roleId,
        RoleAssignmentScopeType scopeType,
        long? scopeId,
        DateTimeOffset? validFrom = null)
    {
        return new RoleAssignment
        {
            TenantId = tenantId,
            AccountId = accountId,
            RoleId = roleId,
            ScopeType = scopeType,
            ScopeId = scopeId,
            ValidFrom = validFrom ?? DateTimeOffset.UtcNow
        };
    }

    public void Revoke(DateTimeOffset? at = null)
    {
        if (ValidTo is not null)
            throw new InvalidOperationException("Assignment is already revoked.");

        ValidTo = at ?? DateTimeOffset.UtcNow;
    }

    void IHasRowVersion.IncrementRowVersion() => RowVersion++;
}
