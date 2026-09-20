namespace Access.Application.Authentication;

/// <summary>Configurable password policy constraints (server-authoritative).</summary>
public sealed class PasswordPolicyOptions
{
    public int MinLength { get; set; } = 12;
    public int MaxLength { get; set; } = 128;
}
