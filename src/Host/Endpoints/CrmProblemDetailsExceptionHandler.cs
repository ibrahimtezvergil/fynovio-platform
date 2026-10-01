using Contracts;
using CRM.Application;
using CRM.Customization;
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
        if (exception is CustomFieldValidationException invalid)
        {
            // Per-field errors in the shared 422 shape (`errors`: field -> messages) so the form places each next to its
            // input; `codes` (field -> machine codes) lets the client word them in the user's language.
            var byField = invalid.Errors.GroupBy(e => e.Field, StringComparer.Ordinal);
            httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Type = "custom_field_invalid",
                Title = "One or more custom field values are invalid.",
                Extensions =
                {
                    ["errors"] = byField.ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray(), StringComparer.Ordinal),
                    ["codes"] = byField.ToDictionary(g => g.Key, g => g.Select(e => e.Code).ToArray(), StringComparer.Ordinal)
                }
            }, cancellationToken);
            return true;
        }

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
            OpportunityRestoreStageRequiredException => (StatusCodes.Status422UnprocessableEntity, "restore_stage_required", exception.Message),
            OpportunityRestoreStageInvalidException => (StatusCodes.Status422UnprocessableEntity, "restore_stage_invalid", exception.Message),
            InvalidPipelineTransitionException => (StatusCodes.Status409Conflict, "invalid_pipeline_transition", exception.Message),
            PipelineConfigurationInvalidException => (StatusCodes.Status409Conflict, "invalid_pipeline_configuration", exception.Message),
            PipelineNotProvisionedException => (StatusCodes.Status409Conflict, "pipeline_not_provisioned", exception.Message),
            CrmSettingsConcurrencyConflictException => (StatusCodes.Status409Conflict, "concurrency_conflict", exception.Message),
            CrmCatalogKeyConflictException => (StatusCodes.Status409Conflict, "catalog_key_conflict", exception.Message),
            CrmPipelineNameConflictException => (StatusCodes.Status409Conflict, "pipeline_name_conflict", exception.Message),
            PipelineValidationException => (StatusCodes.Status422UnprocessableEntity, "pipeline_validation_failed", exception.Message),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "not_found", exception.Message),
            MasterData.Application.IdempotencyKeyReusedException => (StatusCodes.Status409Conflict, "idempotency_key_reused", exception.Message),
            IdempotencyKeyReusedException => (StatusCodes.Status409Conflict, "idempotency_key_reused", exception.Message),
            PrincipalNotAssignableException => (StatusCodes.Status422UnprocessableEntity, "principal_not_assignable", exception.Message),
            CrmAssignmentProviderUnavailableException => (StatusCodes.Status422UnprocessableEntity, "assignment_provider_unavailable", exception.Message),
            PartyNotFoundException => (StatusCodes.Status422UnprocessableEntity, "party_not_found", exception.Message),
            CustomFieldKeyConflictException => (StatusCodes.Status409Conflict, "custom_field_key_conflict", exception.Message),
            CustomFieldLimitExceededException => (StatusCodes.Status422UnprocessableEntity, "field_limit_exceeded", exception.Message),
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
