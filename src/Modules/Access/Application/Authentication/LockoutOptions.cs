namespace Access.Application.Authentication;

/// <summary>Configurable account lockout policy.</summary>
public sealed class LockoutOptions
{
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}
