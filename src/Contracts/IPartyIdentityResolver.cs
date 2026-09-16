namespace Contracts;

/// <summary>Command-precondition surface — must reflect current committed state and
/// resolve through a Party merge chain to the canonical PartyRef. A future
/// CreateOpportunity-shaped command validates against this, never against
/// IPartyDirectory's display-oriented reads (see
/// docs/plans/2026-09-16-masterdata-party-foundation.md §4). Returns the canonical
/// PartyRef, not a bool — a caller who passes a since-merged id must get back the
/// survivor's ref, never create something pointing at a tombstone.</summary>
public interface IPartyIdentityResolver
{
    Task<PartyRef?> ResolveAsync(PartyRef partyRef, CancellationToken cancellationToken = default);

    Task<PartyRef?> ResolveExternalIdentityAsync(
        TenantId tenantId, string provider, string sourceInstanceRef,
        string? externalType, string externalId, CancellationToken cancellationToken = default);
}
