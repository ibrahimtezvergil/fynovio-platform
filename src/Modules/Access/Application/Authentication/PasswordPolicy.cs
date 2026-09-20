namespace Access.Application.Authentication;

/// <summary>Server-authoritative password policy validation. Returns violation codes
/// (not exceptions) to allow the UI to display policy constraints.</summary>
public sealed class PasswordPolicy
{
    private readonly PasswordPolicyOptions _options;

    public PasswordPolicy(PasswordPolicyOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>Validate a password against the policy. Returns a list of violation codes
    /// (empty if valid). Codes: `too_short`, `too_long`, `equals_email_local_part`.</summary>
    public IReadOnlyList<string> Validate(string password, string email)
    {
        if (string.IsNullOrWhiteSpace(password))
            return new[] { "required" };

        var violations = new List<string>();

        if (password.Length < _options.MinLength)
            violations.Add("too_short");

        if (password.Length > _options.MaxLength)
            violations.Add("too_long");

        var emailLocalPart = email.Split('@')[0];
        if (password.Equals(emailLocalPart, StringComparison.OrdinalIgnoreCase))
            violations.Add("equals_email_local_part");

        return violations;
    }
}
