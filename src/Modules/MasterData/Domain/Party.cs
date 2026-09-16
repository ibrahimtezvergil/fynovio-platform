using Contracts;

namespace MasterData.Domain;

/// <summary>Aggregate root for the platform's shared identity concept. Merge stays
/// tombstone-based (no hard delete); MergeInto enforces single-hop only as a guard —
/// the resolution/repoint logic lives in the MergeParty application handler. See
/// docs/plans/2026-09-16-masterdata-party-foundation.md §5.</summary>
public sealed class Party
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public PartyType PartyType { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Surname { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public long? MergedIntoPartyId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Party() { }

    public static Party Create(
        TenantId tenantId, PartyType partyType, string name,
        string? surname = null, string? phone = null, string? email = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (partyType == PartyType.Organization && surname is not null)
            throw new ArgumentException("An organization does not have a surname.", nameof(surname));

        var now = DateTimeOffset.UtcNow;
        return new Party
        {
            TenantId = tenantId,
            PartyType = partyType,
            Name = name,
            Surname = surname,
            Phone = phone,
            Email = email,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Refuses a target that is itself already a tombstone — never resolves it
    /// automatically. The caller (MergeParty handler) must resolve the requested target
    /// to its own canonical first; this guard is what makes that mandatory.</summary>
    public void MergeInto(Party canonical)
    {
        // Reference equality, not Id equality: unsaved entities all default to Id 0, and
        // EF's identity map returns the same tracked instance for the same Id within one
        // DbContext anyway, so this is correct both in domain tests and in the real path.
        if (ReferenceEquals(canonical, this))
            throw new InvalidOperationException("A party cannot merge into itself.");
        if (canonical.MergedIntoPartyId is not null)
            throw new InvalidOperationException("Merge target must already be resolved to its own canonical party.");

        MergedIntoPartyId = canonical.Id;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Called on every existing tombstone that pointed at this party, the
    /// moment this party itself gets merged into a new canonical — keeps every
    /// tombstone exactly one hop from canonical at all times. Internal: only the
    /// MergeParty handler, operating within MasterData, calls this.</summary>
    internal void RepointMergeTarget(long newCanonicalPartyId)
    {
        if (MergedIntoPartyId is null)
            throw new InvalidOperationException("Only a tombstoned party can be repointed.");

        MergedIntoPartyId = newCanonicalPartyId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
