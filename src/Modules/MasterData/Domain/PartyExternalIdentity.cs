using Contracts;

namespace MasterData.Domain;

/// <summary>Provider (system type, e.g. "sap") and source instance (the specific
/// configured connection, e.g. "sap-connection-a") are distinct — a tenant can run
/// multiple instances of the same provider, each with its own overlapping id
/// namespace. See docs/plans/2026-09-16-masterdata-party-foundation.md §6.</summary>
public sealed class PartyExternalIdentity
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public long PartyId { get; private set; }
    public string Provider { get; private set; } = null!;
    public string SourceInstanceRef { get; private set; } = null!;
    public string? ExternalType { get; private set; }
    public string ExternalId { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    private PartyExternalIdentity() { }

    public static PartyExternalIdentity Create(
        TenantId tenantId, long partyId, string provider, string sourceInstanceRef,
        string? externalType, string externalId)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider is required.", nameof(provider));
        if (string.IsNullOrWhiteSpace(sourceInstanceRef))
            throw new ArgumentException("Source instance reference is required.", nameof(sourceInstanceRef));
        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("External id is required.", nameof(externalId));

        return new PartyExternalIdentity
        {
            TenantId = tenantId,
            PartyId = partyId,
            Provider = provider,
            SourceInstanceRef = sourceInstanceRef,
            ExternalType = externalType,
            ExternalId = externalId,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>Called only by the MergeParty handler, reassigning identities from a
    /// merged-away party to the survivor.</summary>
    internal void ReassignTo(long survivorPartyId) => PartyId = survivorPartyId;
}
