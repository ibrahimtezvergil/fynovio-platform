namespace Host.Authentication;

/// <summary>Bound from the "Authentication:Jwt" configuration section. SigningKey must
/// come from the `Authentication__Jwt__SigningKey` environment variable (ASP.NET Core's
/// double-underscore section-path convention) or a secret store in any non-development
/// environment — never commit a production key (AGENTS.md Safety rules).
///
/// SigningKey must be at least 32 bytes when encoded as UTF-8 (HMAC-SHA256 requires ≥32 bytes).
/// Validated at startup via ValidateOnStart.</summary>
public sealed class JwtOptions
{
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string SigningKey { get; init; }

    /// <summary>Access token lifetime in minutes (default 10). Configurable via
    /// "Authentication:Jwt:AccessTokenMinutes".</summary>
    public int AccessTokenMinutes { get; init; } = 10;
}
