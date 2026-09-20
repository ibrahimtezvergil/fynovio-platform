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

    public static IResult Validation(IDictionary<string, string[]> errors) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            type: "validation_error",
            title: "The request is not valid.",
            extensions: new Dictionary<string, object?> { ["errors"] = errors });
}
