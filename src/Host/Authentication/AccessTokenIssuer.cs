using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Host.Authentication;

/// <summary>Issues JWT access tokens for authenticated sessions.
/// Claims: iss, aud, sub (platform ExternalIdentity subject), tid (tenant id),
/// sid (session id), jti (unique id), iat, exp.
/// TTL = configured AccessTokenMinutes (default 10).</summary>
public sealed class AccessTokenIssuer
{
    private readonly JwtOptions _jwtOptions;
    private readonly TimeProvider _timeProvider;

    public AccessTokenIssuer(JwtOptions jwtOptions, TimeProvider? timeProvider = null)
    {
        _jwtOptions = jwtOptions ?? throw new ArgumentNullException(nameof(jwtOptions));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Mint an access token for a session.
    ///
    /// Returns: (token string, expiresInSeconds)</summary>
    public (string Token, int ExpiresInSeconds) IssueToken(
        string subject,
        long tenantId,
        Guid sessionId)
    {
        var now = _timeProvider.GetUtcNow();
        var expiresIn = TimeSpan.FromMinutes(_jwtOptions.AccessTokenMinutes);
        var expiresAt = now.Add(expiresIn);

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("iss", _jwtOptions.Issuer),
            new Claim("aud", _jwtOptions.Audience),
            new Claim("sub", subject),
            new Claim("tid", tenantId.ToString()),
            new Claim("sid", sessionId.ToString()),
            new Claim("jti", Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        var expiresInSeconds = (int)expiresIn.TotalSeconds;

        return (tokenString, expiresInSeconds);
    }
}
