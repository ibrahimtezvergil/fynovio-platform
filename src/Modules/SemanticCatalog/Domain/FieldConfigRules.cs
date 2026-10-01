using System.Text.Json;
using System.Text.RegularExpressions;
using Contracts;

namespace SemanticCatalog.Domain;

/// <summary>Definition-side rules for <see cref="FieldConfig"/> — which members a type allows and how a replacement
/// may differ from what is stored. They live here only; consumers validate *values* against the read model.</summary>
public static partial class FieldConfigRules
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static FieldConfig FromJson(string json) =>
        JsonSerializer.Deserialize<FieldConfig>(json, SerializerOptions) ?? FieldConfig.Empty;

    public static string ToJson(this FieldConfig config) => JsonSerializer.Serialize(config, SerializerOptions);

    /// <summary>Returns a normalized copy (decimal scale defaulted, empty lists dropped) or throws when the config does
    /// not fit <paramref name="type"/>.</summary>
    public static FieldConfig Validate(this FieldConfig config, FieldType type)
    {
        var isSelect = type is FieldType.Select or FieldType.MultiSelect;
        var isNumeric = type is FieldType.Number or FieldType.Decimal;
        var lengthCap = FieldConfig.TextLengthCap(type);

        if (isSelect)
            ValidateOptions(config.Options);
        else if (config.Options is { Count: > 0 })
            throw new ArgumentException("Only select fields can define options.", nameof(config.Options));

        var scale = config.Scale;
        if (type == FieldType.Decimal)
        {
            scale ??= FieldConfig.DefaultDecimalScale;
            if (scale is < 0 or > FieldConfig.MaxDecimalScale)
                throw new ArgumentException($"Decimal scale must be between 0 and {FieldConfig.MaxDecimalScale}.", nameof(config.Scale));
        }
        else if (config.Scale is not null)
            throw new ArgumentException("Only decimal fields can define a scale.", nameof(config.Scale));

        if (isNumeric)
        {
            if (config.Min is { } min && config.Max is { } max && min > max)
                throw new ArgumentException("Minimum cannot be greater than maximum.", nameof(config.Min));
            if (type == FieldType.Number && (config.Min is { } intMin && decimal.Truncate(intMin) != intMin || config.Max is { } intMax && decimal.Truncate(intMax) != intMax))
                throw new ArgumentException("Whole-number limits must be integers.", nameof(config.Min));
            if (type == FieldType.Decimal && (config.Min is { } decMin && decimal.Round(decMin, scale!.Value) != decMin || config.Max is { } decMax && decimal.Round(decMax, scale!.Value) != decMax))
                throw new ArgumentException("Decimal limits cannot have more decimal places than the field's scale.", nameof(config.Min));
        }
        else if (config.Min is not null || config.Max is not null)
            throw new ArgumentException("Only number and decimal fields can define minimum or maximum.", nameof(config.Min));

        if (lengthCap > 0)
        {
            if (config.MaxLength is { } length && (length < 1 || length > lengthCap))
                throw new ArgumentException($"Maximum length must be between 1 and {lengthCap}.", nameof(config.MaxLength));
        }
        else if (config.MaxLength is not null)
            throw new ArgumentException("Only text fields can define a maximum length.", nameof(config.MaxLength));

        return config with { Options = isSelect ? config.Options : null, Scale = scale };
    }

    /// <summary>Options may be added, relabelled and deprecated, never removed; the decimal scale never changes
    /// (stored values were validated against it).</summary>
    public static void EnsureCompatibleReplacement(this FieldConfig current, FieldConfig replacement)
    {
        foreach (var option in current.Options ?? [])
        {
            if (!replacement.HasOption(option.Key))
                throw new ArgumentException($"Option '{option.Key}' cannot be removed; deprecate it instead.", nameof(current.Options));
        }

        if (current.Scale != replacement.Scale)
            throw new ArgumentException("A decimal field's scale cannot change after creation.", nameof(current.Scale));
    }

    private static void ValidateOptions(IReadOnlyList<FieldOption>? options)
    {
        if (options is not { Count: > 0 })
            throw new ArgumentException("A select field needs at least one option.", nameof(options));
        if (options.Count > FieldConfig.MaxOptions)
            throw new ArgumentException($"A select field can have at most {FieldConfig.MaxOptions} options.", nameof(options));
        if (options.All(option => option.IsDeprecated))
            throw new ArgumentException("A select field needs at least one active option.", nameof(options));
        if (options.Select(option => option.Key).Distinct(StringComparer.Ordinal).Count() != options.Count)
            throw new ArgumentException("Option keys must be unique.", nameof(options));

        foreach (var option in options)
        {
            if (option.Key is null || !OptionKeyFormat().IsMatch(option.Key))
                throw new ArgumentException($"Option key '{option.Key}' must be 1–63 lowercase letters, digits or underscores.", nameof(options));
            if (string.IsNullOrWhiteSpace(option.Label) || option.Label.Trim().Length > FieldConfig.MaxOptionLabelLength)
                throw new ArgumentException($"Option '{option.Key}' needs a label of 1–{FieldConfig.MaxOptionLabelLength} characters.", nameof(options));
        }
    }

    [GeneratedRegex(@"^[a-z0-9][a-z0-9_]{0,62}$")]
    private static partial Regex OptionKeyFormat();
}
