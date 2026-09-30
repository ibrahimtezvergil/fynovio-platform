namespace CRM.Customization;

/// <summary>Carries every field error at once so a form can mark all invalid inputs in one round trip.</summary>
public sealed class CustomFieldValidationException(IReadOnlyList<CustomFieldError> errors)
    : ArgumentException($"Custom field values are invalid: {string.Join(", ", errors.Select(error => $"{error.Field} ({error.Code})"))}.")
{
    public IReadOnlyList<CustomFieldError> Errors { get; } = errors;
}
