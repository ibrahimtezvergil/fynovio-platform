using Access.Application;
using Contracts;

namespace Host.Authentication;

/// <summary>Runs after UseAuthentication()/UseAuthorization(). Builds the trusted
/// ActorContext from JWT claims — never from a caller-supplied command body
/// (Contracts.ActorContext's own doc comment; round 3 §4 final pipeline steps 2-3) —
/// and validates the claimed tenant against an active TenantMembership (step 4,
/// "tenant membership/state validation") before any endpoint runs.</summary>
public sealed class ActorContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, PrincipalResolver principalResolver)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var issuer = context.User.FindFirst("iss")?.Value;
        var subject = context.User.FindFirst("sub")?.Value;
        var tenantClaim = context.User.FindFirst("tid")?.Value;

        if (issuer is null || subject is null || tenantClaim is null || !long.TryParse(tenantClaim, out var tenantIdValue))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var principal = new PrincipalRef(issuer, subject);
        var tenantId = new TenantId(tenantIdValue);

        if (!await principalResolver.IsActiveTenantMemberAsync(principal, tenantId, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var correlationId = context.Request.Headers.TryGetValue("X-Correlation-Id", out var header)
            && Guid.TryParse(header, out var parsed)
                ? parsed
                : Guid.NewGuid();

        context.Items["ActorContext"] = new ActorContext(tenantId, principal, correlationId);

        await next(context);
    }
}

public static class HttpContextActorContextExtensions
{
    public static ActorContext GetActorContext(this HttpContext context) =>
        context.Items["ActorContext"] as ActorContext?
            ?? throw new InvalidOperationException(
                "No ActorContext resolved for this request. The endpoint must call RequireAuthorization() so ActorContextMiddleware runs first.");
}
