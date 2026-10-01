namespace Contracts;

/// <summary>A select choice. `Key` is stable and is what a record stores; removing a choice deprecates it instead,
/// so values already stored keep resolving to a label.</summary>
public sealed record FieldOption(string Key, string Label, bool IsDeprecated = false);

/// <summary>The one entity type a `reference` field points at, in the same `(bounded context, entity type)` terms as an
/// <c>ILinkTargetResolver</c>: the resolver is what answers whether a given id is visible to the caller.</summary>
public sealed record FieldTarget(string BoundedContext, string EntityType);

/// <summary>Type-specific shape of a field definition. Every member is optional; the catalog decides which ones a
/// given type allows when a definition is written (definition-side rules live there only). This read model carries
/// what a consumer needs to validate a *value*: option keys, decimal scale, bounds and length caps.</summary>
public sealed record FieldConfig(
    IReadOnlyList<FieldOption>? Options = null,
    int? Scale = null,
    decimal? Min = null,
    decimal? Max = null,
    int? MaxLength = null,
    FieldTarget? Target = null)
{
    public const int MaxOptions = 50;
    public const int MaxOptionLabelLength = 100;
    public const int MaxDecimalScale = 6;
    public const int DefaultDecimalScale = 2;

    public static FieldConfig Empty { get; } = new();

    public static int TextLengthCap(FieldType type) => type switch
    {
        FieldType.Text => 2000,
        FieldType.LongText => 10000,
        _ => 0
    };

    public bool IsOptionActive(string key) => Options?.Any(option => option.Key == key && !option.IsDeprecated) ?? false;

    public bool HasOption(string key) => Options?.Any(option => option.Key == key) ?? false;
}
