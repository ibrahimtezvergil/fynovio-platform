using Collaboration.Application;
using Contracts;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Host.Endpoints;

/// <summary>Maps Collaboration's application-layer exceptions to problem details. It only claims the module's own
/// exception types (all derive from Exception directly, so neither handler can swallow the other's types); domain
/// validation `ArgumentException`s stay with <see cref="CrmProblemDetailsExceptionHandler"/> (400 validation_error).</summary>
public sealed class CollaborationProblemDetailsExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, type, title) = exception switch
        {
            // Record-level denial must be externally indistinguishable from a genuinely missing entry (tenant
            // non-leak rule): same status, same type, and the SAME title CalendarEntryNotFoundException produces —
            // never the raw exception.Message, which would say "was denied" instead of "was not found".
            CalendarEntryAuthorizationDeniedException { DenialStage: AuthorizationDenialStage.Record, EntryId: { } entryId } =>
                (StatusCodes.Status404NotFound, "not_found", NotFoundTitle(entryId)),
            CalendarEntryAuthorizationDeniedException => (StatusCodes.Status403Forbidden, "forbidden", exception.Message),
            CalendarEntryNotFoundException notFound => (StatusCodes.Status404NotFound, "not_found", notFound.Message),
            CalendarEntryConcurrencyConflictException => (StatusCodes.Status409Conflict, "concurrency_conflict", exception.Message),
            IdempotencyKeyReusedException => (StatusCodes.Status409Conflict, "idempotency_key_reused", exception.Message),
            CalendarRangeTooLargeException => (StatusCodes.Status422UnprocessableEntity, "range_too_large", exception.Message),
            _ => (0, (string?)null, (string?)null)
        };

        if (status == 0)
            return false; // not one of ours — let the next handler decide

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = status, Type = type, Title = title },
            cancellationToken);
        return true;
    }

    private static string NotFoundTitle(long entryId) => new CalendarEntryNotFoundException(entryId).Message;
}
