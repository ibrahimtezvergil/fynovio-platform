using Microsoft.AspNetCore.Identity;

namespace Access.Application.Authentication;

/// <summary>Neutral marker type for PasswordHasher&lt;T&gt;. PasswordHasher never reads
/// the user argument; this is just a non-null placeholder.</summary>
internal sealed class PasswordHasherUser;

/// <summary>Wraps Microsoft.AspNetCore.Identity.PasswordHasher&lt;T&gt; for consistent
/// password hashing and verification. Provides a dummy verify method to ensure login attempts
/// for unknown accounts also consume the same time (side-channel resistance).</summary>
public sealed class PasswordService
{
    private readonly PasswordHasher<PasswordHasherUser> _hasher = new();
    private static readonly string DummyHash = new PasswordHasher<PasswordHasherUser>()
        .HashPassword(new PasswordHasherUser(), "dummy");

    /// <summary>Hash a plain-text password for storage.</summary>
    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password is required.", nameof(password));

        return _hasher.HashPassword(new PasswordHasherUser(), password);
    }

    /// <summary>Verify a plain-text password against its hash. Returns Success,
    /// SuccessRehashNeeded, or Failed.</summary>
    public PasswordVerificationResult Verify(string hash, string password)
    {
        if (string.IsNullOrWhiteSpace(hash))
            throw new ArgumentException("Hash is required.", nameof(hash));
        if (string.IsNullOrWhiteSpace(password))
            return PasswordVerificationResult.Failed;

        return _hasher.VerifyHashedPassword(new PasswordHasherUser(), hash, password);
    }

    /// <summary>Verify a password against a dummy hash to provide constant-time-ish
    /// behavior for unknown accounts (mitigates timing attacks).</summary>
    public void VerifyDummy(string password)
    {
        _hasher.VerifyHashedPassword(new PasswordHasherUser(), DummyHash, password);
    }
}
