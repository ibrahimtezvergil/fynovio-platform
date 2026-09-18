namespace Host.Authentication;

/// <summary>Bound from the "Authentication:Jwt" configuration section. SigningKey must
/// come from the `Authentication__Jwt__SigningKey` environment variable (ASP.NET Core's
/// double-underscore section-path convention) or a secret store in any non-development
/// environment — never commit a production key (AGENTS.md Safety rules).</summary>
public sealed class JwtOptions
{
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string SigningKey { get; init; }
}
