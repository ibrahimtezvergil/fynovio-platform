using CRM.Application;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Host.Endpoints;

/// <summary>Maps CRM's application-layer exceptions to the error model architecture
/// plan §15 specifies. Registered via AddExceptionHandler&lt;T&gt;() + AddProblemDetails()
/// so every endpoint gets this for free instead of a repeated try/catch per route.</summary>
public sealed class CrmProblemDetailsExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, type) = exception switch
        {
            OpportunityNotFoundException => (StatusCodes.Status404NotFound, "not_found"),
            OpportunityAuthorizationDeniedException => (StatusCodes.Status403Forbidden, "forbidden"),
            OpportunityConcurrencyConflictException => (StatusCodes.Status409Conflict, "concurrency_conflict"),
            InvalidPipelineTransitionException => (StatusCodes.Status409Conflict, "invalid_pipeline_transition"),
            IdempotencyKeyReusedException => (StatusCodes.Status409Conflict, "idempotency_key_reused"),
            ArgumentException => (StatusCodes.Status400BadRequest, "validation_error"),
            InvalidOperationException => (StatusCodes.Status409Conflict, "illegal_lifecycle_transition"),
            _ => (0, (string?)null)
        };

        if (status == 0)
            return false; // not one of ours — let the default developer/production handler take it

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = status, Type = type, Title = exception.Message },
            cancellationToken);
        return true;
    }
}
