namespace Access.Application.Authentication;

/// <summary>Deliberately shallow: real validation is "can we deliver to it". This only rejects
/// values that cannot possibly be an address, and builds the masked form shown to invitees.</summary>
internal static class EmailAddressRules
{
    private const int MaxLength = 320;

    public static bool IsPlausible(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var trimmed = email.Trim();
        if (trimmed.Length > MaxLength || trimmed.Any(char.IsWhiteSpace) || trimmed.Any(char.IsControl))
            return false;

        var at = trimmed.IndexOf('@');
        if (at <= 0 || at != trimmed.LastIndexOf('@') || at == trimmed.Length - 1)
            return false;

        var domain = trimmed[(at + 1)..];
        return domain.Contains('.') && !domain.StartsWith('.') && !domain.EndsWith('.');
    }

    /// <summary>`jane.doe@example.com` → `j***@example.com`.</summary>
    public static string Mask(string email)
    {
        var at = email.IndexOf('@');
        return at <= 0 ? "***" : $"{email[0]}***{email[at..]}";
    }
}
