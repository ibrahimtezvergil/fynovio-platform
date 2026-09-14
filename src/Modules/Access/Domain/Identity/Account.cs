namespace Access.Domain.Identity;

/// <summary>Platform-global, not tenant-scoped — the one deliberate exception to doc 08's
/// "no tenant-free repository method for tenant records" rule, because an account is not
/// tenant-owned business data; it is the identity a <see cref="TenantMembership"/> points at
/// (docs/schema/identity-access-schema.md §1.1).</summary>
public sealed class Account
{
    public long Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string? Locale { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Account() { }

    public static Account Create(string email, string displayName, string? locale = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        var now = DateTimeOffset.UtcNow;
        return new Account
        {
            Email = email,
            DisplayName = displayName,
            Locale = locale,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Email is profile data, never an identity key — do not add a unique
    /// constraint on it. <see cref="Contracts.PrincipalRef"/>'s own doc comment: "Do not use
    /// mutable email as identity."</summary>
    public void UpdateProfile(string displayName, string? locale = null)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        DisplayName = displayName;
        Locale = locale;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
