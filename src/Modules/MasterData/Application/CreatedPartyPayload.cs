using Contracts;

namespace MasterData.Application;

internal sealed record CreatedPartyPayload(long PartyId, PartyType PartyType);
