namespace SemanticCatalog.Domain;

/// <summary>What a change set item does to a definition.</summary>
public enum FieldOperation
{
    Create,
    Update,
    Deprecate,
    Reactivate
}
