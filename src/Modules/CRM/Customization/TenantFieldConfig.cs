using System.Text.Json;
using System.Text.RegularExpressions;

namespace CRM.Customization;

/// <summary>Type-specific shape of a field definition, stored as the definition's `config` jsonb. Every member
/// is optional; <see cref="Validate"/> decides which ones a given value type allows.</summary>
public sealed partial record TenantFieldConfig(
    IReadOnlyList<TenantFieldOption>? Options = null,
    int? Scale = null,
    decimal? Min = null,
    decimal? Max = null,
    int? MaxLength = null)
{
    public const int MaxOptions = 50;
    public const int MaxOptionLabelLength = 100;
    public const int MaxDecimalScale = 6;
    public const int DefaultDecimalScale = 2;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static TenantFieldConfig Empty { get; } = new();

    public static TenantFieldConfig FromJson(string json) =>
        JsonSerializer.Deserialize<TenantFieldConfig>(json, SerializerOptions) ?? Empty;

    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    public static int TextLengthCap(TenantFieldValueType type) => type switch
    {
        TenantFieldValueType.Text => 2000,
        TenantFieldValueType.LongText => 10000,
        _ => 0
    };

    public bool IsOptionActive(string key) => Options?.Any(option => option.Key == key && !option.IsDeprecated) ?? false;

    public bool HasOption(string key) => Options?.Any(option => option.Key == key) ?? false;

    /// <summary>Returns a normalized copy (decimal scale defaulted, empty lists dropped) or throws when the
    /// config does not fit <paramref name="type"/>.</summary>
    public TenantFieldConfig Validate(TenantFieldValueType type)
    {
        var isSelect = type is TenantFieldValueType.Select or TenantFieldValueType.MultiSelect;
        var isNumeric = type is TenantFieldValueType.Number or TenantFieldValueType.Decimal;
        var lengthCap = TextLengthCap(type);

        if (isSelect)
            ValidateOptions(Options);
        else if (Options is { Count: > 0 })
            throw new ArgumentException("Only select fields can define options.", nameof(Options));

        var scale = Scale;
        if (type == TenantFieldValueType.Decimal)
        {
            scale ??= DefaultDecimalScale;
            if (scale is < 0 or > MaxDecimalScale)
                throw new ArgumentException($"Decimal scale must be between 0 and {MaxDecimalScale}.", nameof(Scale));
        }
        else if (Scale is not null)
            throw new ArgumentException("Only decimal fields can define a scale.", nameof(Scale));

        if (isNumeric)
        {
            if (Min is { } min && Max is { } max && min > max)
                throw new ArgumentException("Minimum cannot be greater than maximum.", nameof(Min));
            if (type == TenantFieldValueType.Number && (Min is { } intMin && decimal.Truncate(intMin) != intMin || Max is { } intMax && decimal.Truncate(intMax) != intMax))
                throw new ArgumentException("Whole-number limits must be integers.", nameof(Min));
            if (type == TenantFieldValueType.Decimal && (Min is { } decMin && decimal.Round(decMin, scale!.Value) != decMin || Max is { } decMax && decimal.Round(decMax, scale!.Value) != decMax))
                throw new ArgumentException("Decimal limits cannot have more decimal places than the field's scale.", nameof(Min));
        }
        else if (Min is not null || Max is not null)
            throw new ArgumentException("Only number and decimal fields can define minimum or maximum.", nameof(Min));

        if (lengthCap > 0)
        {
            if (MaxLength is { } length && (length < 1 || length > lengthCap))
                throw new ArgumentException($"Maximum length must be between 1 and {lengthCap}.", nameof(MaxLength));
        }
        else if (MaxLength is not null)
            throw new ArgumentException("Only text fields can define a maximum length.", nameof(MaxLength));

        return this with { Options = isSelect ? Options : null, Scale = scale };
    }

    /// <summary>Options may be added, relabelled and deprecated, never removed; the decimal scale never changes
    /// (stored values were validated against it).</summary>
    public void EnsureCompatibleReplacement(TenantFieldConfig replacement)
    {
        foreach (var option in Options ?? [])
        {
            if (!replacement.HasOption(option.Key))
                throw new ArgumentException($"Option '{option.Key}' cannot be removed; deprecate it instead.", nameof(Options));
        }

        if (Scale != replacement.Scale)
            throw new ArgumentException("A decimal field's scale cannot change after creation.", nameof(Scale));
    }

    private static void ValidateOptions(IReadOnlyList<TenantFieldOption>? options)
    {
        if (options is not { Count: > 0 })
            throw new ArgumentException("A select field needs at least one option.", nameof(Options));
        if (options.Count > MaxOptions)
            throw new ArgumentException($"A select field can have at most {MaxOptions} options.", nameof(Options));
        if (options.All(option => option.IsDeprecated))
            throw new ArgumentException("A select field needs at least one active option.", nameof(Options));
        if (options.Select(option => option.Key).Distinct(StringComparer.Ordinal).Count() != options.Count)
            throw new ArgumentException("Option keys must be unique.", nameof(Options));

        foreach (var option in options)
        {
            if (option.Key is null || !OptionKeyFormat().IsMatch(option.Key))
                throw new ArgumentException($"Option key '{option.Key}' must be 1–63 lowercase letters, digits or underscores.", nameof(Options));
            if (string.IsNullOrWhiteSpace(option.Label) || option.Label.Trim().Length > MaxOptionLabelLength)
                throw new ArgumentException($"Option '{option.Key}' needs a label of 1–{MaxOptionLabelLength} characters.", nameof(Options));
        }
    }

    [GeneratedRegex(@"^[a-z0-9][a-z0-9_]{0,62}$")]
    private static partial Regex OptionKeyFormat();
}
