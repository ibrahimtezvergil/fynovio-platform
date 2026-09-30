using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CRM.Customization;

/// <summary>Validates and normalizes a record's custom field object against its definitions
/// (adr-tier1-custom-fields.md decisions 4, 6, 7). An update is a full replacement of the active fields;
/// deprecated fields cannot be written but their stored values are carried forward, so deprecation never
/// loses data.</summary>
public static partial class CustomFieldValues
{
    public const int MaxPayloadBytes = 64 * 1024;
    public const int MaxEmailLength = 254;
    public const int MaxPhoneLength = 32;
    public const int MaxUrlLength = 2000;

    /// <returns>The canonical JSON to store, or null when no field has a value.</returns>
    public static string? Normalize(IReadOnlyCollection<TenantFieldDefinition> definitions, JsonElement? input, string? existingJson)
    {
        var errors = new List<CustomFieldError>();
        var existing = Parse(existingJson);
        var provided = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        if (input is { ValueKind: JsonValueKind.Object } inputObject)
        {
            foreach (var property in inputObject.EnumerateObject())
                provided[property.Name] = property.Value;
        }
        else if (input is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) })
        {
            throw new CustomFieldValidationException([new("$", "invalid_type", "Custom fields must be a JSON object.")]);
        }

        var byKey = definitions.ToDictionary(definition => definition.FieldName, StringComparer.Ordinal);
        foreach (var key in provided.Keys)
        {
            if (!byKey.TryGetValue(key, out var definition))
                errors.Add(new(key, "unknown_field", $"'{key}' is not a defined field."));
            else if (!definition.IsActive)
                errors.Add(new(key, "field_deprecated", $"'{key}' is deprecated and can no longer be written."));
        }

        var result = new JsonObject();
        foreach (var definition in definitions.Where(d => d.IsActive).OrderBy(d => d.SortOrder).ThenBy(d => d.FieldName, StringComparer.Ordinal))
        {
            var hasValue = provided.TryGetValue(definition.FieldName, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
            JsonNode? normalized = null;
            if (hasValue)
                normalized = NormalizeValue(definition, value, existing.GetValueOrDefault(definition.FieldName), errors);
            else if (definition.IsRequired)
                errors.Add(new(definition.FieldName, "required", $"'{definition.Label}' is required."));

            if (normalized is not null)
                result[definition.FieldName] = normalized;
        }

        // Carried forward untouched: deprecated fields, and any stored key without a definition (definitions are
        // never deleted, so this only guards against data loss, it is not an expected path).
        foreach (var (key, value) in existing)
        {
            if (!byKey.TryGetValue(key, out var definition) || !definition.IsActive)
                result[key] = JsonNode.Parse(value.GetRawText());
        }

        if (errors.Count > 0)
            throw new CustomFieldValidationException(errors);
        if (result.Count == 0)
            return null;

        var json = result.ToJsonString();
        if (Encoding.UTF8.GetByteCount(json) > MaxPayloadBytes)
            throw new CustomFieldValidationException([new("$", "payload_too_large", $"Custom field values exceed {MaxPayloadBytes / 1024} KB.")]);
        return json;
    }

    /// <summary>Keys whose stored value differs — what the outbox fact reports (never the values themselves).</summary>
    public static IReadOnlyList<string> ChangedKeys(string? beforeJson, string? afterJson)
    {
        var before = Parse(beforeJson);
        var after = Parse(afterJson);
        return before.Keys.Union(after.Keys, StringComparer.Ordinal)
            .Where(key => !before.TryGetValue(key, out var old) || !after.TryGetValue(key, out var current) || old.GetRawText() != current.GetRawText())
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyDictionary<string, JsonElement> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        return document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.Ordinal);
    }

    private static JsonNode? NormalizeValue(TenantFieldDefinition definition, JsonElement value, JsonElement? stored, List<CustomFieldError> errors)
    {
        var key = definition.FieldName;
        var config = definition.Config;

        CustomFieldError Error(string code, string message) => new(key, code, message);
        JsonNode? Fail(string code, string message)
        {
            errors.Add(Error(code, message));
            return null;
        }

        switch (definition.FieldType)
        {
            case TenantFieldValueType.Text:
            case TenantFieldValueType.LongText:
                {
                    if (value.ValueKind != JsonValueKind.String)
                        return Fail("invalid_type", $"'{definition.Label}' must be text.");
                    var text = value.GetString()!;
                    if (string.IsNullOrWhiteSpace(text))
                        return RequiredOrNothing(definition, errors);
                    var cap = config.MaxLength ?? TenantFieldConfig.TextLengthCap(definition.FieldType);
                    return text.Length > cap ? Fail("too_long", $"'{definition.Label}' can be at most {cap} characters.") : JsonValue.Create(text);
                }
            case TenantFieldValueType.Number:
                {
                    if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var number))
                        return Fail("invalid_type", $"'{definition.Label}' must be a whole number.");
                    return InRange(number, config) ? JsonValue.Create(number) : Fail("out_of_range", RangeMessage(definition, config));
                }
            case TenantFieldValueType.Decimal:
                {
                    if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var number))
                        return Fail("invalid_type", $"'{definition.Label}' must be a number.");
                    var scale = config.Scale ?? TenantFieldConfig.DefaultDecimalScale;
                    if (decimal.Round(number, scale) != number)
                        return Fail("invalid_value", $"'{definition.Label}' can have at most {scale} decimal places.");
                    return InRange(number, config) ? JsonValue.Create(number) : Fail("out_of_range", RangeMessage(definition, config));
                }
            case TenantFieldValueType.Boolean:
                return value.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? JsonValue.Create(value.GetBoolean())
                    : Fail("invalid_type", $"'{definition.Label}' must be true or false.");
            case TenantFieldValueType.Date:
                {
                    if (value.ValueKind != JsonValueKind.String
                        || !DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                        return Fail("invalid_type", $"'{definition.Label}' must be a date (YYYY-MM-DD).");
                    return JsonValue.Create(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                }
            case TenantFieldValueType.Select:
                {
                    if (value.ValueKind != JsonValueKind.String)
                        return Fail("invalid_type", $"'{definition.Label}' must be one option key.");
                    var option = value.GetString()!;
                    var storedOption = stored is { ValueKind: JsonValueKind.String } s ? s.GetString() : null;
                    return OptionError(config, option, keptFromStored: option == storedOption) is { } code
                        ? Fail(code, OptionMessage(definition, option, code))
                        : JsonValue.Create(option);
                }
            case TenantFieldValueType.MultiSelect:
                {
                    if (value.ValueKind != JsonValueKind.Array || value.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
                        return Fail("invalid_type", $"'{definition.Label}' must be a list of option keys.");
                    var options = value.EnumerateArray().Select(item => item.GetString()!).ToArray();
                    if (options.Length == 0)
                        return RequiredOrNothing(definition, errors);
                    if (options.Distinct(StringComparer.Ordinal).Count() != options.Length)
                        return Fail("invalid_value", $"'{definition.Label}' lists an option more than once.");
                    var storedOptions = stored is { ValueKind: JsonValueKind.Array } array
                        ? array.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!).ToHashSet(StringComparer.Ordinal)
                        : [];
                    foreach (var option in options)
                    {
                        if (OptionError(config, option, keptFromStored: storedOptions.Contains(option)) is { } code)
                            return Fail(code, OptionMessage(definition, option, code));
                    }
                    return new JsonArray(options.Select(option => (JsonNode?)JsonValue.Create(option)).ToArray());
                }
            case TenantFieldValueType.Email:
                return FormattedText(definition, value, MaxEmailLength, EmailFormat(), "an e-mail address", errors);
            case TenantFieldValueType.Phone:
                return FormattedText(definition, value, MaxPhoneLength, PhoneFormat(), "a phone number", errors);
            case TenantFieldValueType.Url:
                {
                    if (value.ValueKind != JsonValueKind.String)
                        return Fail("invalid_type", $"'{definition.Label}' must be a web address.");
                    var text = value.GetString()!.Trim();
                    if (text.Length == 0)
                        return RequiredOrNothing(definition, errors);
                    if (text.Length > MaxUrlLength)
                        return Fail("too_long", $"'{definition.Label}' can be at most {MaxUrlLength} characters.");
                    return Uri.TryCreate(text, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                        ? JsonValue.Create(text)
                        : Fail("invalid_value", $"'{definition.Label}' must be an http or https address.");
                }
            default:
                return Fail("invalid_type", $"'{definition.Label}' has an unsupported type.");
        }
    }

    private static JsonNode? FormattedText(TenantFieldDefinition definition, JsonElement value, int maxLength, Regex format, string description, List<CustomFieldError> errors)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            errors.Add(new(definition.FieldName, "invalid_type", $"'{definition.Label}' must be {description}."));
            return null;
        }

        var text = value.GetString()!.Trim();
        if (text.Length == 0)
            return RequiredOrNothing(definition, errors);
        if (text.Length > maxLength)
        {
            errors.Add(new(definition.FieldName, "too_long", $"'{definition.Label}' can be at most {maxLength} characters."));
            return null;
        }
        if (!format.IsMatch(text))
        {
            errors.Add(new(definition.FieldName, "invalid_value", $"'{definition.Label}' must be {description}."));
            return null;
        }
        return JsonValue.Create(text);
    }

    /// <summary>An empty string or empty list means "no value"; that is only an error for a required field.</summary>
    private static JsonNode? RequiredOrNothing(TenantFieldDefinition definition, List<CustomFieldError> errors)
    {
        if (definition.IsRequired)
            errors.Add(new(definition.FieldName, "required", $"'{definition.Label}' is required."));
        return null;
    }

    /// <summary>A deprecated option is refused for new writes but may be kept by a record that already holds it.</summary>
    private static string? OptionError(TenantFieldConfig config, string option, bool keptFromStored) =>
        !config.HasOption(option) ? "invalid_option"
        : !config.IsOptionActive(option) && !keptFromStored ? "option_deprecated"
        : null;

    private static string OptionMessage(TenantFieldDefinition definition, string option, string code) => code == "invalid_option"
        ? $"'{option}' is not an option of '{definition.Label}'."
        : $"Option '{option}' of '{definition.Label}' is deprecated.";

    private static bool InRange(decimal value, TenantFieldConfig config) =>
        (config.Min is not { } min || value >= min) && (config.Max is not { } max || value <= max);

    private static string RangeMessage(TenantFieldDefinition definition, TenantFieldConfig config) =>
        $"'{definition.Label}' must be between {config.Min?.ToString(CultureInfo.InvariantCulture) ?? "-∞"} and {config.Max?.ToString(CultureInfo.InvariantCulture) ?? "∞"}.";

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailFormat();

    [GeneratedRegex(@"^\+?[0-9][0-9 ()\-.]{2,30}$")]
    private static partial Regex PhoneFormat();
}
