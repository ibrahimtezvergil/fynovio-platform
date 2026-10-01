namespace CRM.Customization;

/// <summary>The snake_case wire/storage name of each value type — one mapping shared by the database column
/// (CHECK ck_tenant_field_definitions_field_type), the API and the web client.</summary>
public static class TenantFieldValueTypeNames
{
    private static readonly IReadOnlyDictionary<TenantFieldValueType, string> Names = new Dictionary<TenantFieldValueType, string>
    {
        [TenantFieldValueType.Text] = "text",
        [TenantFieldValueType.LongText] = "long_text",
        [TenantFieldValueType.Number] = "number",
        [TenantFieldValueType.Decimal] = "decimal",
        [TenantFieldValueType.Boolean] = "boolean",
        [TenantFieldValueType.Date] = "date",
        [TenantFieldValueType.Select] = "select",
        [TenantFieldValueType.MultiSelect] = "multi_select",
        [TenantFieldValueType.Email] = "email",
        [TenantFieldValueType.Phone] = "phone",
        [TenantFieldValueType.Url] = "url"
    };

    private static readonly IReadOnlyDictionary<string, TenantFieldValueType> Types =
        Names.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    public static string ToName(TenantFieldValueType type) =>
        Names.TryGetValue(type, out var name) ? name : throw new ArgumentOutOfRangeException(nameof(type));

    public static TenantFieldValueType Parse(string name) =>
        Types.TryGetValue(name, out var type) ? type : throw new ArgumentException($"Unknown field type '{name}'.", nameof(name));
}
