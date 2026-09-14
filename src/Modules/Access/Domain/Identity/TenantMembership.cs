using Contracts;

namespace Access.Domain.Identity;

public enum MembershipStatus
{
    Invited,
    Active,
    Disabled
}

/// <summary>Backs Identity's `Invite`/`DisableMembership` commands (doc 08). Membership (are
/// you in this tenant, and are you active/invited/disabled) is a different question from
/// authorization (what can you do, and where) — role never lives on this entity, see
/// <see cref="Access.Domain.Authorization.RoleAssignment"/> and
/// docs/schema/identity-access-schema.md §"Role is never a column on tenant_memberships".</summary>
public sealed class TenantMembership : IHasRowVersion
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public long AccountId { get; private set; }
    public MembershipStatus Status { get; private set; }
    public DateTimeOffset InvitedAt { get; private set; }
    public DateTimeOffset? JoinedAt { get; private set; }
    public DateTimeOffset? DisabledAt { get; private set; }
    public long RowVersion { get; private set; } = 1;

    private TenantMembership() { }

    public static TenantMembership Invite(TenantId tenantId, long accountId)
    {
        return new TenantMembership
        {
            TenantId = tenantId,
            AccountId = accountId,
            Status = MembershipStatus.Invited,
            InvitedAt = DateTimeOffset.UtcNow
        };
    }

    public void Activate()
    {
        if (Status != MembershipStatus.Invited)
            throw new InvalidOperationException($"Cannot activate a membership in status {Status}.");

        Status = MembershipStatus.Active;
        JoinedAt = DateTimeOffset.UtcNow;
    }

    public void Disable()
    {
        if (Status == MembershipStatus.Disabled)
            throw new InvalidOperationException("Membership is already disabled.");

        Status = MembershipStatus.Disabled;
        DisabledAt = DateTimeOffset.UtcNow;
    }

    void IHasRowVersion.IncrementRowVersion() => RowVersion++;
}
