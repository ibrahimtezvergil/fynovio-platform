namespace SemanticCatalog.Domain;

/// <summary>What a change set item does to a definition (a field or a view). The names are what the database stores.</summary>
public enum ChangeOperation
{
    Create,
    Update,
    Deprecate,
    Reactivate
}
