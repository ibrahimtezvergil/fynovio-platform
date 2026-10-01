namespace Contracts;

/// <summary>The value types a catalog field can have. No money type on purpose: money needs one declared rounding
/// point, which belongs to a typed aggregate (adr-tier1-custom-fields.md decision 3).</summary>
public enum FieldType
{
    Text,
    LongText,
    Number,
    Decimal,
    Boolean,
    Date,
    Select,
    MultiSelect,
    Email,
    Phone,
    Url
}

/// <summary>The snake_case wire/storage name of each type — one mapping shared by the database CHECK, the API and
/// the web client.</summary>
public static class FieldTypeNames
{
    private static readonly IReadOnlyDictionary<FieldType, string> Names = new Dictionary<FieldType, string>
    {
        [FieldType.Text] = "text",
        [FieldType.LongText] = "long_text",
        [FieldType.Number] = "number",
        [FieldType.Decimal] = "decimal",
        [FieldType.Boolean] = "boolean",
        [FieldType.Date] = "date",
        [FieldType.Select] = "select",
        [FieldType.MultiSelect] = "multi_select",
        [FieldType.Email] = "email",
        [FieldType.Phone] = "phone",
        [FieldType.Url] = "url"
    };

    private static readonly IReadOnlyDictionary<string, FieldType> Types =
        Names.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    public static string ToName(FieldType type) =>
        Names.TryGetValue(type, out var name) ? name : throw new ArgumentOutOfRangeException(nameof(type));

    public static FieldType Parse(string name) =>
        Types.TryGetValue(name, out var type) ? type : throw new ArgumentException($"Unknown field type '{name}'.", nameof(name));
}
