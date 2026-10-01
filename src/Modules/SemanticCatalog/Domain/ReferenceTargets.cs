using Contracts;

namespace SemanticCatalog.Domain;

/// <summary>The entity types a `reference` field may point at (adr-semantic-catalog-changeset.md S-6). v1 allows exactly one: a
/// Party, answered by the `masterdata/party` link resolver. An allow-listed target without a registered resolver would make every
/// write a 422, so Host.Tests asserts each target here has a resolver. Party *values* stay in MasterData (OD-6); an opportunity
/// only stores the id.</summary>
public static class ReferenceTargets
{
    public static readonly IReadOnlyList<FieldTarget> Allowed = [new FieldTarget("masterdata", "party")];

    public static bool IsAllowed(FieldTarget target) => Allowed.Contains(target);
}
