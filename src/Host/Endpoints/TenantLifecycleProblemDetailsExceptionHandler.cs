using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TenantLifecycle.Application;

namespace Host.Endpoints;

public sealed class TenantLifecycleProblemDetailsExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, type) = exception switch
        {
            CompanySettingsAuthorizationDeniedException => (StatusCodes.Status403Forbidden, "forbidden"),
            CompanySettingsNotFoundException => (StatusCodes.Status404NotFound, "not_found"),
            CompanySettingsConcurrencyConflictException => (StatusCodes.Status409Conflict, "concurrency_conflict"),
            IdempotencyKeyReusedException => (StatusCodes.Status409Conflict, "idempotency_key_reused"),
            _ => (0, (string?)null)
        };
        if (status == 0)
            return false;

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = status, Type = type, Title = exception.Message }, cancellationToken);
        return true;
    }
}
