using Access.Application;
using Access.Application.Authentication;
using Contracts;

namespace Host.Authentication;

/// <summary>Runs after UseAuthentication()/UseAuthorization(). Builds the trusted
/// ActorContext from JWT claims — never from a caller-supplied command body
/// (Contracts.ActorContext's own doc comment; round 3 §4 final pipeline steps 2-3) —
/// validates the session id (if present/required) against active sessions, and validates
/// the claimed tenant against an active TenantMembership (step 4,
/// "tenant membership/state validation") before any endpoint runs.
///
/// Session id (sid) check happens before membership check: revoked/expired session → 401,
/// before checking whether the membership is active (which would be 403).</summary>
public sealed class ActorContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        PrincipalResolver principalResolver,
        SessionValidator sessionValidator,
        SessionHostOptions sessionOptions)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var issuer = context.User.FindFirst("iss")?.Value;
        var subject = context.User.FindFirst("sub")?.Value;
        var tenantClaim = context.User.FindFirst("tid")?.Value;
        var sessionIdClaim = context.User.FindFirst("sid")?.Value;
        // MapInboundClaims is false (Program.cs), so these claim names are preserved as-is
        // from the token rather than remapped to long .NET/WS-Fed URIs; if that flag is ever
        // flipped back to true, these lookups will silently stop matching and every
        // authenticated request will 401 here.

        if (issuer is null || subject is null || tenantClaim is null || !long.TryParse(tenantClaim, out var tenantIdValue))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // Session id validation (if required or present)
        if (!string.IsNullOrEmpty(sessionIdClaim) || sessionOptions.RequireSessionClaim)
        {
            // When RequireSessionClaim is true, sid must be present; when false, it's optional
            if (sessionOptions.RequireSessionClaim && string.IsNullOrEmpty(sessionIdClaim))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            // If sid is present, validate it
            if (!string.IsNullOrEmpty(sessionIdClaim))
            {
                if (!Guid.TryParse(sessionIdClaim, out var sessionId))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                // Resolve account id to check session validity
                var principal = new PrincipalRef(issuer, subject);
                var accountId = await principalResolver.ResolveAccountIdAsync(principal, context.RequestAborted);
                if (accountId is null)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                // Validate session is active (not revoked, not expired)
                if (!await sessionValidator.IsActiveAsync(sessionId, accountId.Value, context.RequestAborted))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }
        }

        var principal2 = new PrincipalRef(issuer, subject);
        var tenantId = new TenantId(tenantIdValue);

        if (!await principalResolver.IsActiveTenantMemberAsync(principal2, tenantId, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var correlationId = context.Request.Headers.TryGetValue("X-Correlation-Id", out var header)
            && Guid.TryParse(header, out var parsed)
                ? parsed
                : Guid.NewGuid();

        context.Items["ActorContext"] = new ActorContext(tenantId, principal2, correlationId);

        await next(context);
    }
}
