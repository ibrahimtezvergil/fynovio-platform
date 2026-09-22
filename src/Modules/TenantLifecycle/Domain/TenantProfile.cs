using System.Net.Mail;
using Contracts;

namespace TenantLifecycle.Domain;

/// <summary>The tenant-owned company identity and operating defaults. This is not an Organization hierarchy or a user profile.</summary>
public sealed class TenantProfile
{
    public TenantId TenantId { get; private set; }
    public string DisplayName { get; private set; } = null!;
    public string? LegalName { get; private set; }
    public string? TaxNumber { get; private set; }
    public string? TaxOffice { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Address { get; private set; }
    public string Timezone { get; private set; } = null!;
    public string CurrencyCode { get; private set; } = null!;
    public long RowVersion { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private TenantProfile() { }

    public static TenantProfile Provision(TenantId tenantId, TenantProfileDetails details)
    {
        var profile = new TenantProfile { TenantId = tenantId, CreatedAt = DateTimeOffset.UtcNow, RowVersion = 1 };
        profile.Apply(details, touch: false);
        profile.UpdatedAt = profile.CreatedAt;
        return profile;
    }

    public void Replace(TenantProfileDetails details) => Apply(details, touch: true);

    private void Apply(TenantProfileDetails details, bool touch)
    {
        ArgumentNullException.ThrowIfNull(details);
        DisplayName = RequiredSingleLine(details.DisplayName, 2, 120, nameof(details.DisplayName));
        LegalName = OptionalSingleLine(details.LegalName, 160, nameof(details.LegalName));
        TaxNumber = OptionalSingleLine(details.TaxNumber, 32, nameof(details.TaxNumber));
        TaxOffice = OptionalSingleLine(details.TaxOffice, 120, nameof(details.TaxOffice));
        Email = NormalizeEmail(details.Email);
        Phone = OptionalSingleLine(details.Phone, 32, nameof(details.Phone));
        Address = OptionalText(details.Address, 500, nameof(details.Address));
        Timezone = ValidateTimezone(details.Timezone);
        CurrencyCode = NormalizeCurrency(details.CurrencyCode);

        if (touch)
        {
            RowVersion++;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private static string RequiredSingleLine(string value, int min, int max, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value != value.Trim() || value.Length < min || value.Length > max || value.Any(IsLineBreak))
            throw new ArgumentException($"Value must be a trimmed single line of {min} to {max} characters.", parameterName);
        return value;
    }

    private static string? OptionalSingleLine(string? value, int max, string parameterName) =>
        value is null ? null : RequiredSingleLine(value, 1, max, parameterName);

    private static string? OptionalText(string? value, int max, string parameterName)
    {
        if (value is null)
            return null;
        if (value != value.Trim() || value.Length > max || value.Any(character => char.IsControl(character) && character is not ('\r' or '\n')))
            throw new ArgumentException($"Value must be trimmed text of at most {max} characters without control characters.", parameterName);
        return value;
    }

    private static string? NormalizeEmail(string? value)
    {
        if (value is null)
            return null;
        if (value != value.Trim() || value.Length > 254 || !MailAddress.TryCreate(value, out var address) || address.Address != value)
            throw new ArgumentException("Email must be a normalized address of at most 254 characters.", nameof(value));
        return value.ToLowerInvariant();
    }

    private static string NormalizeCurrency(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != 3 || value.Any(c => c is < 'A' or > 'Z'))
            throw new ArgumentException("Currency code must be a three-letter upper-case ISO 4217 code.", nameof(value));
        return value;
    }

    private static string ValidateTimezone(string value)
    {
        var timezone = RequiredSingleLine(value, 1, 64, nameof(value));
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return timezone;
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ArgumentException("Timezone must be an IANA identifier known to the runtime.", nameof(value));
        }
        catch (InvalidTimeZoneException)
        {
            throw new ArgumentException("Timezone must be an IANA identifier known to the runtime.", nameof(value));
        }
    }

    private static bool IsLineBreak(char value) => value is '\r' or '\n' or '\u2028' or '\u2029' || char.IsControl(value);
}
