using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Host.Authentication;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Host.Tests.Authentication;

public sealed class AccessTokenIssuerTests
{
    private const string SigningKey = "dev-only-signing-key-not-for-production-use-32-bytes-min";

    private static readonly JwtOptions Options = new()
    {
        Issuer = "https://dev.fynovio.local",
        Audience = "fynovio-platform",
        SigningKey = SigningKey,
        AccessTokenMinutes = 10
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Same parameters as `Program.cs` uses to validate bearer tokens.</summary>
    private static TokenValidationParameters ValidationParameters(JwtOptions options) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = options.Issuer,
        ValidateAudience = true,
        ValidAudience = options.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
        ValidateLifetime = true,
        NameClaimType = "sub",
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    private static JwtSecurityToken Read(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact]
    public void Token_carries_exactly_the_specified_claims_as_single_values()
    {
        var sessionId = Guid.NewGuid();
        var issuer = new AccessTokenIssuer(Options);

        var (token, _) = issuer.IssueToken("opaque-subject", 42, sessionId);
        var payload = Read(token).Payload;

        Assert.Equal("https://dev.fynovio.local", Assert.IsType<string>(payload["iss"]));
        Assert.Equal("opaque-subject", Assert.IsType<string>(payload["sub"]));
        Assert.Equal("42", Assert.IsType<string>(payload["tid"]));
        Assert.Equal(sessionId.ToString(), Assert.IsType<string>(payload["sid"]));
        Assert.True(Guid.TryParse(Assert.IsType<string>(payload["jti"]), out _));
        Assert.Equal(new[] { "fynovio-platform" }, Read(token).Audiences.ToArray());
        Assert.True(payload.ContainsKey("iat"), "iat claim is required by the design");
        Assert.True(payload.ContainsKey("exp"));
    }

    [Fact]
    public void Lifetime_is_the_configured_number_of_minutes_and_iat_is_issue_time()
    {
        var now = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        var issuer = new AccessTokenIssuer(Options, new FixedTimeProvider(now));

        var (token, expiresIn) = issuer.IssueToken("s", 1, Guid.NewGuid());
        var payload = Read(token).Payload;

        Assert.Equal(600, expiresIn);
        Assert.Equal(now.AddMinutes(10).ToUnixTimeSeconds(), Convert.ToInt64(payload["exp"]));
        Assert.Equal(now.ToUnixTimeSeconds(), Convert.ToInt64(payload["iat"]));
    }

    [Fact]
    public void Token_is_accepted_by_the_real_validation_parameters()
    {
        var issuer = new AccessTokenIssuer(Options);
        var (token, _) = issuer.IssueToken("s", 7, Guid.NewGuid());

        var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }
            .ValidateToken(token, ValidationParameters(Options), out _);

        Assert.Equal("7", principal.FindFirst("tid")?.Value);
        Assert.Equal("s", principal.FindFirst("sub")?.Value);
    }

    [Fact]
    public void Expired_wrong_key_wrong_issuer_and_wrong_audience_tokens_are_rejected()
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var parameters = ValidationParameters(Options);

        var expired = new AccessTokenIssuer(Options, new FixedTimeProvider(DateTimeOffset.UtcNow.AddHours(-1)))
            .IssueToken("s", 1, Guid.NewGuid()).Token;
        Assert.ThrowsAny<SecurityTokenException>(() => handler.ValidateToken(expired, parameters, out _));

        var otherKey = new JwtOptions { Issuer = Options.Issuer, Audience = Options.Audience, SigningKey = new string('x', 40) };
        var forged = new AccessTokenIssuer(otherKey).IssueToken("s", 1, Guid.NewGuid()).Token;
        Assert.ThrowsAny<SecurityTokenException>(() => handler.ValidateToken(forged, parameters, out _));

        var wrongIssuer = new JwtOptions { Issuer = "https://evil.example", Audience = Options.Audience, SigningKey = SigningKey };
        Assert.ThrowsAny<SecurityTokenException>(() =>
            handler.ValidateToken(new AccessTokenIssuer(wrongIssuer).IssueToken("s", 1, Guid.NewGuid()).Token, parameters, out _));

        var wrongAudience = new JwtOptions { Issuer = Options.Issuer, Audience = "someone-else", SigningKey = SigningKey };
        Assert.ThrowsAny<SecurityTokenException>(() =>
            handler.ValidateToken(new AccessTokenIssuer(wrongAudience).IssueToken("s", 1, Guid.NewGuid()).Token, parameters, out _));
    }

    [Fact]
    public void Every_token_gets_a_fresh_jti()
    {
        var issuer = new AccessTokenIssuer(Options);
        var sessionId = Guid.NewGuid();

        var first = Read(issuer.IssueToken("s", 1, sessionId).Token).Payload["jti"];
        var second = Read(issuer.IssueToken("s", 1, sessionId).Token).Payload["jti"];

        Assert.NotEqual(first, second);
    }
}
