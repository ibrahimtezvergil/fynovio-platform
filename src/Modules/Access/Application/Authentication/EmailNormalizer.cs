using System.Globalization;

namespace Access.Application.Authentication;

/// <summary>Normalizes email addresses for consistent hashing and comparison:
/// trim + NFC normalization + lowercase invariant. Used for uniqueness of
/// account_credentials.login_email_normalized.</summary>
public sealed class EmailNormalizer
{
    public static string Normalize(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        return email
            .Trim()
            .Normalize(System.Text.NormalizationForm.FormC)
            .ToLowerInvariant();
    }
}
