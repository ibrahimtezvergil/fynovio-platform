namespace Contracts;

/// <summary>Party = Person | Organization. Lives here, not in MasterData.Domain, so
/// PartyRef/PartyDirectoryEntry can expose it without any module taking a project
/// reference to MasterData (see docs/plans/2026-09-16-masterdata-party-foundation.md §3).</summary>
public enum PartyType
{
    Person,
    Organization
}
