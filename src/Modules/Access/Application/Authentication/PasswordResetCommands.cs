namespace Access.Application.Authentication;

public sealed record RequestPasswordResetCommand(string Email, string? CorrelationId = null, string? IpHash = null);

public enum PasswordResetRequestStatus
{
    /// <summary>The only answer: the same for known, unknown, locked and malformed addresses.</summary>
    Accepted
}

public sealed record RequestPasswordResetResult(PasswordResetRequestStatus Status = PasswordResetRequestStatus.Accepted);

public sealed record ResetPasswordCommand(string Token, string NewPassword, string? CorrelationId = null, string? IpHash = null);

public enum ResetPasswordStatus
{
    Completed,
    /// <summary>Unknown/malformed/wrong-secret/wrong-purpose/expired/consumed/revoked token, or a token whose account state
    /// does not fit its purpose (a reset for an account without a password, a setup for one that already has one).</summary>
    InvalidOrExpiredToken,
    /// <summary>The token is NOT consumed.</summary>
    PolicyViolation
}

public sealed record ResetPasswordResult(ResetPasswordStatus Status, IReadOnlyList<string>? PolicyViolations = null);
