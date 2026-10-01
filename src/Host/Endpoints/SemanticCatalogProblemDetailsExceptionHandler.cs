using Contracts;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SemanticCatalog.Application;

namespace Host.Endpoints;

/// <summary>Maps the Semantic Catalog's exceptions to the same statuses and error codes the CRM handler produced for
/// the tier-1 field endpoints, so the move is invisible to clients (adr-semantic-catalog-changeset.md S-2). Registered
/// before <see cref="CrmProblemDetailsExceptionHandler"/>, whose generic arms would otherwise catch these types.</summary>
public sealed class SemanticCatalogProblemDetailsExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, type) = exception switch
        {
            // Settings denials are never record-level, so there is no 404-for-403 disguise here (same as CRM's settings).
            CatalogAuthorizationDeniedException => (StatusCodes.Status403Forbidden, "forbidden"),
            CatalogConcurrencyConflictException => (StatusCodes.Status409Conflict, "concurrency_conflict"),
            FieldKeyConflictException => (StatusCodes.Status409Conflict, "custom_field_key_conflict"),
            FieldLimitExceededException => (StatusCodes.Status422UnprocessableEntity, "field_limit_exceeded"),
            IdempotencyKeyReusedException => (StatusCodes.Status409Conflict, "idempotency_key_reused"),
            ViewKeyConflictException => (StatusCodes.Status409Conflict, "view_key_conflict"),
            ViewLimitExceededException => (StatusCodes.Status422UnprocessableEntity, "view_limit_exceeded"),
            ViewColumnUnknownException => (StatusCodes.Status422UnprocessableEntity, "view_column_unknown"),
            ViewColumnDeprecatedException => (StatusCodes.Status422UnprocessableEntity, "view_column_deprecated"),
            _ => (0, (string?)null)
        };

        if (status == 0)
            return false;

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Type = type, Title = exception.Message }, cancellationToken);
        return true;
    }
}
