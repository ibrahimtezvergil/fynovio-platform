using Contracts;

namespace Access.Application.Authentication;

/// <summary>A tenant administrator invites an e-mail address. The account itself is NOT created
/// here — only when the invitation is accepted (no duplicate same-address accounts, nothing to
/// enumerate).</summary>
public sealed record CreateInvitationCommand(
    ActorContext Actor,
    string Email,
    string? DisplayName = null,
    string? Locale = null,
    string? IpHash = null,
    string? IdempotencyKey = null,
    string? RoleKey = null);

public enum CreateInvitationStatus
{
    /// <summary>Identical whether or not the address already has an account or membership.</summary>
    Accepted,
    Forbidden,
    InvalidEmail
}

public sealed record CreateInvitationResult(CreateInvitationStatus Status);
