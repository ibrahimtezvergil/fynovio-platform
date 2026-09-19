using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Host.Tests;

/// <summary>Mints tokens with the same signing key appsettings.Development.json's
/// Authentication:Jwt section declares — this is what "tests mint their own tokens"
/// means in Task 4's scope note; it is not a login flow.</summary>
public static class JwtTestTokenFactory
{
    public const string Issuer = "https://dev.fynovio.local";
    public const string Audience = "fynovio-platform";
    public const string SigningKey = "dev-only-signing-key-not-for-production-use-32-bytes-min";

    public static string Create(string subject, long tenantId)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: [new Claim("sub", subject), new Claim("tid", tenantId.ToString())],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
