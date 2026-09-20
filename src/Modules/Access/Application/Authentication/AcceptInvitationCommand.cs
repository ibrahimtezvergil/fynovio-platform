namespace Access.Application.Authentication;

public sealed record AcceptInvitationCommand(
    string Token,
    string Password,
    string? DisplayName = null,
    string? CorrelationId = null,
    string? IpHash = null,
    string? UserAgentHash = null);

public enum AcceptInvitationStatus
{
    /// <summary>The membership is Active and a session was created — <see cref="AcceptInvitationResult.Session"/> is set.</summary>
    Accepted,
    /// <summary>Unknown/malformed/wrong-secret/wrong-purpose/expired/consumed/revoked token, or an invitation into a
    /// tenant where the account's membership was disabled (a disabled membership is never re-activated by an invite).</summary>
    InvalidOrExpiredToken,
    /// <summary>The address already has a credential and the supplied password did not verify. The token is NOT consumed.</summary>
    InvalidCredentials,
    /// <summary>A new account's password broke the policy. The token is NOT consumed.</summary>
    PolicyViolation
}

/// <summary><see cref="Session"/> has the shape of a login result (`Authenticated` or `TenantSelectionRequired`).</summary>
public sealed record AcceptInvitationResult(
    AcceptInvitationStatus Status,
    AuthenticateResult? Session = null,
    IReadOnlyList<string>? PolicyViolations = null);
