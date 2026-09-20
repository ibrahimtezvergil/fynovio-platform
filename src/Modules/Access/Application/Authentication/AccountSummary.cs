namespace Access.Application.Authentication;

/// <summary>The account fields the sign-in/session responses expose to the client.
/// Comes from the stored `Account`, never from what the caller submitted.</summary>
public sealed record AccountSummary(long Id, string Email, string DisplayName, string? Locale);
