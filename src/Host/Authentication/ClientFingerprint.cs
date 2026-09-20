using System.Security.Cryptography;
using System.Text;

namespace Host.Authentication;

/// <summary>Turns client identifiers (IP address, user agent) into keyed, non-reversible
/// fingerprints for the security event log: the log can correlate repeated activity from the
/// same client without ever storing the raw value. HMAC-SHA256 with a key derived from the JWT
/// signing key under a fixed domain-separation label, hex, truncated to 32 characters.</summary>
public sealed class ClientFingerprint
{
    private const string DomainSeparationLabel = "fynovio.client-fingerprint.v1";
    private const int HexLength = 32;

    private readonly byte[] _key;

    public ClientFingerprint(JwtOptions jwtOptions)
    {
        ArgumentNullException.ThrowIfNull(jwtOptions);
        _key = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(jwtOptions.SigningKey),
            Encoding.UTF8.GetBytes(DomainSeparationLabel));
    }

    /// <summary>Fingerprint of an arbitrary value, or null when there is nothing to hash.</summary>
    public string? Hash(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        var mac = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(mac)[..HexLength].ToLowerInvariant();
    }

    public string? HashIp(HttpContext context) => Hash(context.Connection.RemoteIpAddress?.ToString());

    public string? HashUserAgent(HttpContext context) => Hash(context.Request.Headers.UserAgent.ToString());
}
