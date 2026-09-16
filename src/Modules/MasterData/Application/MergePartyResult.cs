namespace MasterData.Application;

public sealed record MergePartyResult(long SourcePartyId, long CanonicalPartyId, bool Replayed);
