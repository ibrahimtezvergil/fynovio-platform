using Access.Application.Authentication;
using System.ComponentModel.DataAnnotations;

namespace Host.Authentication;

/// <summary>Host-layer authentication configuration, bound from "Authentication:" section in appsettings.
/// Includes session options, CORS origin allowlist, rate limiting, password policy, and lockout policy.
/// Validated at startup via ValidateOnStart.</summary>
public sealed class AuthenticationHostOptions
{
    /// <summary>Session and refresh token configuration (RefreshIdleDays, RefreshAbsoluteDays,
    /// RefreshGraceSeconds, PlatformIssuer, plus Host-only CookieName, CookiePath,
    /// AllowInsecureCookieInDevelopment, RequireSessionClaim).</summary>
    public required SessionHostOptions Session { get; init; }

    /// <summary>Explicit list of allowed CORS origins (empty by default = no CORS).
    /// Must be absolute http(s) URLs.</summary>
    public string[] AllowedOrigins { get; init; } = [];

    /// <summary>Base URL for public-facing links in emails (invite, password reset).
    /// Must be an absolute http(s) URL when set (required in non-Development environments).
    /// Built from configuration; never from request headers (prevents host-header injection).</summary>
    public string? PublicAppBaseUrl { get; init; }

    /// <summary>Password policy configuration (MinLength, MaxLength).</summary>
    public required PasswordPolicyOptions Password { get; init; }

    /// <summary>Account lockout policy (MaxFailedAttempts, LockoutMinutes).</summary>
    public required LockoutOptions Lockout { get; init; }

    /// <summary>Rate limiting policy configurations (login, refresh, forgot, token, password, public).</summary>
    public required RateLimitingOptions RateLimiting { get; init; }
}

/// <summary>Session-related authentication options, including Host-specific cookie configuration.</summary>
public sealed class SessionHostOptions
{
    /// <summary>Idle expiry window in days (sliding via rotation). Default 7.</summary>
    public int RefreshIdleDays { get; init; } = 7;

    /// <summary>Absolute expiry window in days (not sliding). Default 30.</summary>
    public int RefreshAbsoluteDays { get; init; } = 30;

    /// <summary>Grace period in seconds for benign double-submits of a rotated token
    /// (returns 409 instead of revoking the session). Default 5.</summary>
    public int RefreshGraceSeconds { get; init; } = 5;

    /// <summary>JWT issuer for the platform IdP (used when creating ExternalIdentity for password-based sign-in).
    /// Defaults to Authentication:Jwt:Issuer if not explicitly set.</summary>
    public string? PlatformIssuer { get; init; }

    /// <summary>Refresh cookie name. Default "fynovio_rt".</summary>
    public string CookieName { get; init; } = "fynovio_rt";

    /// <summary>Refresh cookie path. Default "/auth" (the direct backend path);
    /// Vite proxy / production reverse proxy uses "/api/auth" but strips the "/api" prefix
    /// before passing to the backend, so this must match the backend's mounted path.</summary>
    public string CookiePath { get; init; } = "/auth";

    /// <summary>Allow insecure (non-Secure) cookies in Development environment.
    /// Must be false in non-Development environments. Default false.</summary>
    public bool AllowInsecureCookieInDevelopment { get; init; }

    /// <summary>Require valid session id (sid claim) in access tokens.
    /// Default true; set to false in test environments for legacy token compatibility.</summary>
    public bool RequireSessionClaim { get; init; } = true;
}

/// <summary>Rate limiting policy configurations, keyed by endpoint/purpose.</summary>
public sealed class RateLimitingOptions
{
    /// <summary>Login endpoint rate limit (default 30 per minute per IP).</summary>
    public int LoginPerMinute { get; init; } = 30;

    /// <summary>Refresh endpoint rate limit (default 60 per minute per IP).</summary>
    public int RefreshPerMinute { get; init; } = 60;

    /// <summary>Forgot-password endpoint rate limit (default 20 per hour per IP).</summary>
    public int ForgotPerHour { get; init; } = 20;

    /// <summary>Token validation/accept endpoint rate limit (default 20 per 15 minutes per IP).</summary>
    public int TokenPer15Minutes { get; init; } = 20;

    /// <summary>Password change endpoint rate limit (default 10 per 15 minutes per IP).</summary>
    public int PasswordPer15Minutes { get; init; } = 10;

    /// <summary>Public endpoints (config, registration when enabled) rate limit (default 120 per minute per IP).</summary>
    public int PublicPerMinute { get; init; } = 120;
}
