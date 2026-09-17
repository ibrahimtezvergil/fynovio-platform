namespace Contracts;

/// <summary>A closed term in an `AccessScope.AnyOf` union. Phase 1.5 implements only
/// `OwnedBy` (round 1 §10.2, round 3 §8 IMPLEMENT NOW #14) — new terms (InOrgNodes,
/// InTerritories, SharedWith) are added as new sealed records when their fact provider
/// exists, never guessed at today.</summary>
public abstract record ScopeTerm
{
    public sealed record OwnedBy(PrincipalRef Principal) : ScopeTerm;
}
