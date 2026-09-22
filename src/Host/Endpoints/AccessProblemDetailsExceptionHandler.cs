using Access.Application;
using Access.Idempotency;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Host.Endpoints;

/// <summary>Maps Access commands used by the workspace console to the platform problem contract.</summary>
public sealed class AccessProblemDetailsExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, type) = exception switch
        {
            AuthorizationDeniedException => (StatusCodes.Status403Forbidden, "forbidden"),
            IdempotencyKeyReusedException => (StatusCodes.Status409Conflict, "idempotency_key_reused"),
            _ => (0, (string?)null)
        };

        if (status == 0)
            return false;

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Type = type, Title = exception.Message }, cancellationToken);
        return true;
    }
}
