namespace Contracts;

/// <summary>Strongly-typed reference to a Party — not a generic EntityRef. Party is
/// statically known platform-wide (there is only ever one), so EntityRef's
/// BoundedContext/EntityType genericity would carry two always-constant columns for no
/// benefit. See docs/plans/2026-09-16-masterdata-party-foundation.md §2.</summary>
public readonly record struct PartyRef
{
    public TenantId TenantId { get; }
    public long PartyId { get; }

    public PartyRef(TenantId tenantId, long partyId)
    {
        if (partyId <= 0)
            throw new ArgumentOutOfRangeException(nameof(partyId), "PartyId must be a positive identifier.");

        TenantId = tenantId;
        PartyId = partyId;
    }

    public override string ToString() => $"Party/{PartyId}@{TenantId}";
}
