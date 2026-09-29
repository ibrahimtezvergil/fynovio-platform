namespace Contracts;

/// <summary>Display-oriented snapshot of a Party, returned by IPartyDirectory — never
/// a tracked entity. MasterData.Application maps its Party into this before it crosses
/// the module boundary.</summary>
public sealed record PartyDirectoryEntry(PartyRef PartyRef, PartyType PartyType, string Name, string? Surname, string? Email, string? Phone = null);
