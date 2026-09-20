namespace Host.Endpoints;

/// <summary>RFC 7807 responses for the authentication endpoints. The `type` is a short
/// snake_case code (same style as the CRM handler); titles are constant so that responses for
/// different internal causes are byte-identical (no account/tenant enumeration).</summary>
public static class AuthProblems
{
    public static IResult InvalidCredentials() =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, type: "invalid_credentials", title: "Invalid credentials.");

    public static IResult SessionInvalid() =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, type: "session_invalid", title: "The session is not valid.");

    public static IResult CsrfRejected() =>
        Results.Problem(statusCode: StatusCodes.Status403Forbidden, type: "csrf_rejected", title: "Request rejected.");

    public static IResult TenantNotPermitted() =>
        Results.Problem(statusCode: StatusCodes.Status403Forbidden, type: "tenant_not_permitted", title: "The tenant is not available for this account.");

    public static IResult RefreshConflict() =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, type: "refresh_conflict", title: "The refresh token was just used.");

    public static IResult RateLimited() =>
        Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, type: "rate_limited", title: "Too many requests.");

    /// <summary>The single answer for an unknown, malformed, expired, already used or revoked invitation/reset token.</summary>
    public static IResult InvalidOrExpiredToken() =>
        Results.Problem(statusCode: StatusCodes.Status400BadRequest, type: "invalid_or_expired_token", title: "The link is invalid or has expired.");

    public static IResult PasswordPolicyViolation(IReadOnlyList<string>? violations) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            type: "password_policy_violation",
            title: "The password does not meet the policy.",
            extensions: new Dictionary<string, object?> { ["violations"] = violations ?? [] });

    /// <summary>400, not 401: a mistyped current password must never look like an expired session (the SPA would try to refresh and sign the user out).</summary>
    public static IResult InvalidCurrentPassword() =>
        Results.Problem(statusCode: StatusCodes.Status400BadRequest, type: "invalid_current_password", title: "The current password is not correct.");

    public static IResult Forbidden() =>
        Results.Problem(statusCode: StatusCodes.Status403Forbidden, type: "forbidden", title: "You are not allowed to do this.");

    public static IResult Conflict() =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, type: "conflict", title: "The request conflicted with another change. Please retry.");

    public static IResult Validation(IDictionary<string, string[]> errors) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            type: "validation_error",
            title: "The request is not valid.",
            extensions: new Dictionary<string, object?> { ["errors"] = errors });
}
