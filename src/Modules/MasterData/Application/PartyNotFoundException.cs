namespace MasterData.Application;

public sealed class PartyNotFoundException : InvalidOperationException
{
    public PartyNotFoundException(long partyId)
        : base($"Party {partyId} was not found.")
    {
    }
}
