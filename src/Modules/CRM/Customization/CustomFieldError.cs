namespace CRM.Customization;

/// <summary>`Code` is a stable machine value (`required`, `unknown_field`, `field_deprecated`, `invalid_type`,
/// `invalid_value`, `out_of_range`, `too_long`, `invalid_option`, `option_deprecated`, `payload_too_large`) the
/// client maps to its own message; `Message` is an English fallback.</summary>
public sealed record CustomFieldError(string Field, string Code, string Message);
