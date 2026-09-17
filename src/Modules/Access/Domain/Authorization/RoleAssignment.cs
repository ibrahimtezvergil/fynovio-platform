using Contracts;

namespace Access.Domain.Authorization;

/// <summary>Grants, separate from membership. Every assignment is tenant-wide in
/// Phase 1.5 — `ScopeType`/`ScopeId` are removed, not reserved-and-denied
/// (gap-closure §1): Organization's fact provider doesn't exist, and a perpetually
/// deny-only enum value is dead weight. Org-node scope is added additively in the
/// migration that ships alongside a real Organization module. `Network` (cross-tenant)
/// is gone unconditionally (round 3 §6) — cross-tenant access is never modeled as an
/// ordinary assignment scope.</summary>
public sealed class RoleAssignment : IHasRowVersion
{
    public const string SourceManual = "manual";
    public const string SourceBootstrap = "bootstrap";

    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public PrincipalType PrincipalType { get; private set; }
    public long AccountId { get; private set; }
    public long RoleId { get; private set; }
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset? ValidTo { get; private set; }
    public string Source { get; private set; } = null!;
    public long GrantedByAccountId { get; private set; }
    public string? Reason { get; private set; }
    public long RowVersion { get; private set; } = 1;

    private RoleAssignment() { }

    public static RoleAssignment Grant(
        TenantId tenantId,
        long accountId,
        long roleId,
        long grantedByAccountId,
        string source,
        string? reason = null,
        DateTimeOffset? validFrom = null)
    {
        if (source is not (SourceManual or SourceBootstrap))
            throw new ArgumentException("Source must be 'manual' or 'bootstrap'.", nameof(source));

        return new RoleAssignment
        {
            TenantId = tenantId,
            PrincipalType = Authorization.PrincipalType.User,
            AccountId = accountId,
            RoleId = roleId,
            GrantedByAccountId = grantedByAccountId,
            Source = source,
            Reason = reason,
            ValidFrom = validFrom ?? DateTimeOffset.UtcNow
        };
    }

    public void Revoke(DateTimeOffset? at = null)
    {
        if (ValidTo is not null)
            throw new InvalidOperationException("Assignment is already revoked.");

        var revokedAt = at ?? DateTimeOffset.UtcNow;
        if (revokedAt < ValidFrom)
            throw new ArgumentOutOfRangeException(nameof(at), "Cannot revoke before the assignment's valid_from.");

        ValidTo = revokedAt;
    }

    public bool IsActiveAt(DateTimeOffset at) => ValidFrom <= at && (ValidTo is null || at < ValidTo);

    void IHasRowVersion.IncrementRowVersion() => RowVersion++;
}
