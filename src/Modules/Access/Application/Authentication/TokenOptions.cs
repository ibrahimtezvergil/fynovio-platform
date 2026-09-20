namespace Access.Application.Authentication;

/// <summary>Lifetimes of the single-use account tokens. Configurable; the defaults are
/// recommendations, not binding values (plan §14 D9).</summary>
public sealed class TokenOptions
{
    public int InviteDays { get; set; } = 7;
    public int PasswordResetMinutes { get; set; } = 30;
    public int PasswordSetupHours { get; set; } = 24;
}
