namespace SemanticCatalog.Domain;

/// <summary>The bounded contexts that may own definitions, and what each may own and which PDP actions govern it
/// (adr-semantic-catalog-changeset.md S-4). Authorization reuses the owner's existing settings actions, so no
/// capability-template change is needed; an unknown owner is refused rather than guessed.</summary>
public static class CatalogOwners
{
    public const string Crm = "crm";
    public const string Opportunity = "opportunity";

    public sealed record Owner(string Context, IReadOnlyList<string> ObjectTypes, string ReadAction, string WriteAction, string ResourceType);

    /// <summary>The built-in columns of an object's table that a shared view may place. The opportunity list's two placeholder columns
    /// (service and interaction duration) are not here: they show nothing yet.</summary>
    private static readonly IReadOnlyDictionary<(string, string), IReadOnlyList<string>> BuiltInColumns = new Dictionary<(string, string), IReadOnlyList<string>>
    {
        [(Crm, Opportunity)] = ["id", "createdAt", "updatedAt", "party", "owner", "needs", "status", "stage", "amount", "totalAmount", "expiryDate"]
    };

    private static readonly IReadOnlyList<Owner> Known =
    [
        // Same action keys and resource descriptor as CRM's settings handlers (agreement proven by a test).
        new Owner(Crm, [Opportunity], "crm.settings.read", "crm.settings.update", "CrmSettings")
    ];

    public static bool IsKnown(string ownerContext, string objectType) =>
        Known.Any(owner => owner.Context == ownerContext && owner.ObjectTypes.Contains(objectType));

    public static IReadOnlyList<string> BuiltInViewColumns(string ownerContext, string objectType) =>
        BuiltInColumns.GetValueOrDefault((ownerContext, objectType)) ?? [];

    public static Owner Require(string ownerContext) =>
        Known.SingleOrDefault(owner => owner.Context == ownerContext)
            ?? throw new ArgumentException($"Unknown definition owner '{ownerContext}'.", nameof(ownerContext));
}
