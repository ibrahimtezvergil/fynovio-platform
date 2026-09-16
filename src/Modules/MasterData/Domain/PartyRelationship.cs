using Contracts;

namespace MasterData.Domain;

public enum PartyRelationshipType
{
    WorksFor,
    BranchOf
}

public enum PartyRelationshipStatus
{
    Active,
    Ended
}

/// <summary>Typed graph edge between two Parties. Deliberately narrow vocabulary for
/// now (WorksFor, BranchOf) — new types get added only against a validated request,
/// same discipline as OpportunityStatus's closed enum. Temporal model is status +
/// nullable dates, not raw effective_from/to: different relationship types have
/// genuinely different date-certainty (WorksFor usually knows both dates; BranchOf
/// usually doesn't know when, only that it currently is). See
/// docs/plans/2026-09-16-masterdata-party-foundation.md §3.</summary>
public sealed class PartyRelationship
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public long FromPartyId { get; private set; }
    public long ToPartyId { get; private set; }
    public PartyRelationshipType RelationshipType { get; private set; }
    public PartyRelationshipStatus Status { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public string? JobTitle { get; private set; }
    public string? WorkEmail { get; private set; }
    public string? WorkPhone { get; private set; }
    public string? Metadata { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private PartyRelationship() { }

    public static PartyRelationship Create(
        TenantId tenantId, long fromPartyId, long toPartyId, PartyRelationshipType relationshipType,
        DateTimeOffset? startedAt = null, string? jobTitle = null, string? workEmail = null, string? workPhone = null)
    {
        if (fromPartyId == toPartyId)
            throw new ArgumentException("A party cannot have a relationship with itself.");

        var now = DateTimeOffset.UtcNow;
        return new PartyRelationship
        {
            TenantId = tenantId,
            FromPartyId = fromPartyId,
            ToPartyId = toPartyId,
            RelationshipType = relationshipType,
            Status = PartyRelationshipStatus.Active,
            StartedAt = startedAt,
            JobTitle = jobTitle,
            WorkEmail = workEmail,
            WorkPhone = workPhone,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void End(DateTimeOffset? endedAt = null)
    {
        if (Status == PartyRelationshipStatus.Ended)
            throw new InvalidOperationException("Relationship is already ended.");

        Status = PartyRelationshipStatus.Ended;
        EndedAt = endedAt ?? DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
