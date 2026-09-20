namespace Access.Application.Authentication;

/// <summary>Configurable session and refresh token lifetimes and behavior.</summary>
public sealed class SessionOptions
{
    /// <summary>Idle expiry window in days (sliding via rotation).</summary>
    public int RefreshIdleDays { get; set; } = 7;

    /// <summary>Absolute expiry window in days (not sliding).</summary>
    public int RefreshAbsoluteDays { get; set; } = 30;

    /// <summary>Grace period in seconds for benign double-submits of a rotated token
    /// (returns 409 instead of revoking the session).</summary>
    public int RefreshGraceSeconds { get; set; } = 5;

    /// <summary>Required JWT issuer — used to identify the platform IdP and create
    /// the ExternalIdentity for password-based sign-in.</summary>
    public string PlatformIssuer { get; set; } = null!;
}
