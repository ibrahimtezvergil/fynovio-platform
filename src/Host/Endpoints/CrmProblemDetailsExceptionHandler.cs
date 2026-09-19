using Contracts;
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
        var (status, type, title) = exception switch
        {
            // Record-level denial must be externally indistinguishable from a genuinely
            // missing resource (tenant non-leak rule) — same status, same type, and the
            // SAME title OpportunityNotFoundException would produce, never the raw
            // exception.Message (which would leak "was denied" instead of "was not
            // found"). DenialStage.Record only ever occurs once a resource was already
            // loaded, so OpportunityId is always set here. See
            // docs/architecture-analysis/2026-09-19-crm-phase2-authorization-delta-and-entry-stage-resolution.md.
            OpportunityAuthorizationDeniedException { DenialStage: AuthorizationDenialStage.Record } ex =>
                (StatusCodes.Status404NotFound, "not_found", $"Opportunity {ex.OpportunityId} was not found."),
            OpportunityNotFoundException => (StatusCodes.Status404NotFound, "not_found", exception.Message),
            OpportunityAuthorizationDeniedException => (StatusCodes.Status403Forbidden, "forbidden", exception.Message),
            OpportunityConcurrencyConflictException => (StatusCodes.Status409Conflict, "concurrency_conflict", exception.Message),
            InvalidPipelineTransitionException => (StatusCodes.Status409Conflict, "invalid_pipeline_transition", exception.Message),
            PipelineConfigurationInvalidException => (StatusCodes.Status409Conflict, "invalid_pipeline_configuration", exception.Message),
            IdempotencyKeyReusedException => (StatusCodes.Status409Conflict, "idempotency_key_reused", exception.Message),
            ArgumentException => (StatusCodes.Status400BadRequest, "validation_error", exception.Message),
            InvalidOperationException => (StatusCodes.Status409Conflict, "illegal_lifecycle_transition", exception.Message),
            _ => (0, (string?)null, (string?)null)
        };

        if (status == 0)
            return false; // not one of ours — let the default developer/production handler take it

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = status, Type = type, Title = title },
            cancellationToken);
        return true;
    }
}
