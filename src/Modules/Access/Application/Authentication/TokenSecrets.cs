using System.Security.Cryptography;
using System.Text;

namespace Access.Application.Authentication;

/// <summary>Generates cryptographically random refresh token secrets and computes their
/// SHA-256 hashes for storage. Tokens are 32-byte random secrets (256 bits) encoded as
/// base64url. Only the hash is stored in the database.</summary>
public sealed class TokenSecrets
{
    /// <summary>Generate a new 32-byte random token and return both the token and its
    /// SHA-256 hash (hex-encoded).</summary>
    public static (string token, string hash) GenerateAndHash()
    {
        var randomBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        var token = Base64UrlEncode(randomBytes);
        var hash = HashToken(token);

        return (token, hash);
    }

    /// <summary>Compute SHA-256 hash of a token for constant-time comparison.</summary>
    public static string HashToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token is required.", nameof(token));

        using (var sha256 = SHA256.Create())
        {
            var bytes = Encoding.UTF8.GetBytes(token);
            var hashBytes = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }

    /// <summary>Constant-time comparison of token hashes.</summary>
    public static bool HashesEqual(string hash1, string hash2)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(hash1),
            Encoding.UTF8.GetBytes(hash2));
    }

    /// <summary>Encode bytes as base64url (RFC 4648 without padding).</summary>
    private static string Base64UrlEncode(byte[] input)
    {
        var standard = Convert.ToBase64String(input);
        return standard
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>Decode base64url string to bytes. Adds padding if needed.</summary>
    public static byte[] Base64UrlDecode(string input)
    {
        var standard = input
            .Replace('-', '+')
            .Replace('_', '/');

        // Add padding if needed
        switch (input.Length % 4)
        {
            case 2:
                standard += "==";
                break;
            case 3:
                standard += "=";
                break;
        }

        return Convert.FromBase64String(standard);
    }
}
