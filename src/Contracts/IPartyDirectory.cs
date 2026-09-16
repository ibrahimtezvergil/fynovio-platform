namespace Contracts;

/// <summary>Read/display-oriented Party lookup — cheap, side-effect-free, no
/// strong-consistency requirement. Never a substitute for IPartyIdentityResolver's
/// command preconditions (see docs/plans/2026-09-16-masterdata-party-foundation.md §4).
/// Batch lookup is load-bearing, not a nicety: a list of N opportunities needs one
/// round-trip to MasterData via GetPartiesAsync, not N.</summary>
public interface IPartyDirectory
{
    Task<PartyDirectoryEntry?> GetPartyAsync(PartyRef partyRef, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<PartyRef, PartyDirectoryEntry>> GetPartiesAsync(
        IReadOnlyCollection<PartyRef> partyRefs, CancellationToken cancellationToken = default);
}
