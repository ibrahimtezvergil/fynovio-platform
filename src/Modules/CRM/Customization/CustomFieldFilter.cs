using System.Text.Json.Nodes;

namespace CRM.Customization;

/// <summary>Equality filters over a record's custom field values, as one jsonb containment document
/// (`custom_fields @> {...}`) so a GIN index on the column serves it. Only the option-like types are filterable:
/// select and multi_select (by option key) and boolean. Deprecated fields and deprecated options stay filterable,
/// because their stored values stay readable.</summary>
public static class CustomFieldFilter
{
    public const int MaxFilters = 5;

    public static bool IsFilterable(TenantFieldValueType type) =>
        type is TenantFieldValueType.Select or TenantFieldValueType.MultiSelect or TenantFieldValueType.Boolean;

    /// <returns>The containment document, or null when there is nothing to filter on.</returns>
    public static string? ToContainmentJson(IReadOnlyCollection<TenantFieldDefinition> definitions, IReadOnlyDictionary<string, string>? filters)
    {
        if (filters is null || filters.Count == 0)
            return null;
        if (filters.Count > MaxFilters)
            throw new CustomFieldValidationException([new("$", "too_many_filters", $"At most {MaxFilters} custom field filters are allowed.")]);

        var byKey = definitions.ToDictionary(definition => definition.FieldName, StringComparer.Ordinal);
        var errors = new List<CustomFieldError>();
        var document = new JsonObject();

        foreach (var (key, value) in filters)
        {
            if (!byKey.TryGetValue(key, out var definition))
            {
                errors.Add(new(key, "unknown_field", $"'{key}' is not a defined field."));
                continue;
            }

            switch (definition.FieldType)
            {
                case TenantFieldValueType.Select when definition.Config.HasOption(value):
                    document[key] = value;
                    break;
                case TenantFieldValueType.MultiSelect when definition.Config.HasOption(value):
                    document[key] = new JsonArray(value);
                    break;
                case TenantFieldValueType.Select or TenantFieldValueType.MultiSelect:
                    errors.Add(new(key, "invalid_option", $"'{value}' is not an option of '{key}'."));
                    break;
                case TenantFieldValueType.Boolean when value is "true" or "false":
                    document[key] = value == "true";
                    break;
                case TenantFieldValueType.Boolean:
                    errors.Add(new(key, "invalid_value", $"'{key}' filters on true or false."));
                    break;
                default:
                    errors.Add(new(key, "not_filterable", $"'{key}' cannot be used as a list filter."));
                    break;
            }
        }

        if (errors.Count > 0)
            throw new CustomFieldValidationException(errors);
        return document.ToJsonString();
    }
}
