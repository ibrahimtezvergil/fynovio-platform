using Contracts;
using Access.Application;
using CRM.Application;
using CRM.Domain;
using Host.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Host.Endpoints;

public static class CrmSettingsEndpoints
{
    public static void MapCrmSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/crm/settings").RequireAuthorization();
        group.MapGet("", async (HttpContext httpContext, GetCrmSettingsHandler handler,
            EnsureTenantAdministratorActionsHandler ensureAdministratorActions, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            await ensureAdministratorActions.HandleAsync(new EnsureTenantAdministratorActionsCommand(
                actor.TenantId, actor.Principal, [CrmActionKeys.SettingsRead, CrmActionKeys.SettingsUpdate], actor.CorrelationId), cancellationToken);
            return Results.Ok(await handler.HandleAsync(new GetCrmSettingsQuery(actor.TenantId, actor.Principal, actor.CorrelationId), cancellationToken));
        });
        group.MapPut("", async (CrmSettingsRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            HttpContext httpContext, UpdateCrmSettingsHandler handler, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("Idempotency-Key is required.");
            var actor = httpContext.GetActorContext();
            var result = await handler.HandleAsync(new UpdateCrmSettingsCommand(actor.TenantId, actor.Principal, request.ExpectedVersion,
                request.DefaultPipelineDefinitionId, ParseEnum<OpportunityCreationMode>(request.OpportunityCreationMode), request.DefaultOpportunityTypeId, request.RequireLostReason,
                request.RequireWonLine, ParseEnum<AssignmentMode>(request.DefaultAssignmentMode), ParseEnum<AssignmentPolicy>(request.AssignmentPolicy),
                request.DefaultPrincipalIssuer is { Length: > 0 } issuer && request.DefaultPrincipalSubject is { Length: > 0 } subject ? new PrincipalRef(issuer, subject) : null,
                request.DefaultTeamId, request.DefaultTerritoryId, idempotencyKey, actor.CorrelationId), cancellationToken);
            return Results.Ok(result);
        });
        group.MapPost("/pipelines/drafts", async (CreatePipelineDraftRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            HttpContext httpContext, CreatePipelineDraftHandler handler, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            return Results.Created("/crm/settings", await handler.HandleAsync(new CreatePipelineDraftCommand(actor.TenantId, actor.Principal,
                request.PipelineDefinitionId, request.Name, request.ExpectedRowVersion, request.ExpectedLatestVersionNumber, request.Stages,
                request.EnforceAllowedTransitions, request.AllowedTransitions, RequiredKey(idempotencyKey), actor.CorrelationId), cancellationToken));
        });
        group.MapGet("/pipelines/{pipelineId:long}/versions/{versionId:long}/validate", async (long pipelineId, long versionId,
            HttpContext httpContext, ValidatePipelineDraftHandler handler, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            return Results.Ok(await handler.HandleAsync(actor.TenantId, pipelineId, versionId, actor.Principal, actor.CorrelationId, cancellationToken));
        });
        group.MapPost("/pipelines/{pipelineId:long}/versions/{versionId:long}/publish", async (long pipelineId, long versionId,
            PublishPipelineRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, HttpContext httpContext,
            PublishPipelineVersionHandler handler, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            return Results.Ok(await handler.HandleAsync(new PublishPipelineVersionCommand(actor.TenantId, actor.Principal, pipelineId,
                versionId, request.ExpectedPipelineRowVersion, RequiredKey(idempotencyKey), actor.CorrelationId), cancellationToken));
        });
        group.MapPut("/pipelines/{pipelineId:long}/lifecycle", async (long pipelineId, SetPipelineLifecycleRequest request,
            [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, HttpContext httpContext,
            SetPipelineLifecycleHandler handler, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            return Results.Ok(await handler.HandleAsync(new SetPipelineLifecycleCommand(actor.TenantId, actor.Principal, pipelineId,
                request.ExpectedRowVersion, request.IsActive, request.Archive, request.Restore, RequiredKey(idempotencyKey), actor.CorrelationId), cancellationToken));
        });
        group.MapPost("/catalog/{kind}", async (string kind, CrmCatalogRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            HttpContext httpContext, ManageCrmCatalogHandler handler, CancellationToken cancellationToken) =>
        {
            var catalogKind = ParseCatalogKind(kind);
            var actor = httpContext.GetActorContext();
            var result = await handler.HandleAsync(new ManageCrmCatalogCommand(actor.TenantId, actor.Principal, catalogKind, null, 0,
                request.Key ?? string.Empty, request.Name, request.Category, request.AveragePrice, ConfigurationStatus.Active,
                RequiredKey(idempotencyKey), actor.CorrelationId), cancellationToken);
            return Results.Created($"/crm/settings/catalog/{kind}/{result.Item.Id}", result);
        });
        group.MapPut("/catalog/{kind}/{id:long}", async (string kind, long id, CrmCatalogRequest request,
            [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, HttpContext httpContext,
            ManageCrmCatalogHandler handler, CancellationToken cancellationToken) =>
        {
            var catalogKind = ParseCatalogKind(kind);
            var actor = httpContext.GetActorContext();
            return Results.Ok(await handler.HandleAsync(new ManageCrmCatalogCommand(actor.TenantId, actor.Principal, catalogKind, id,
                request.ExpectedVersion, request.Key ?? string.Empty, request.Name, request.Category, request.AveragePrice,
                ParseCatalogStatus(request.Status), RequiredKey(idempotencyKey), actor.CorrelationId), cancellationToken));
        });
    }

    private static string RequiredKey(string key) => string.IsNullOrWhiteSpace(key) ? throw new ArgumentException("Idempotency-Key is required.") : key;
    private static ConfigurationStatus ParseCatalogStatus(string status) => Enum.TryParse<ConfigurationStatus>(status, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
        ? parsed : throw new ArgumentException("Catalog status must be Active, Inactive or Archived.");
    private static TEnum ParseEnum<TEnum>(string value) where TEnum : struct, Enum => Enum.TryParse<TEnum>(value, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
        ? parsed : throw new ArgumentException($"Unknown {typeof(TEnum).Name} value.");
    private static CrmCatalogKind ParseCatalogKind(string kind) => kind.ToLowerInvariant() switch
    {
        "opportunity-types" => CrmCatalogKind.OpportunityType,
        "lost-reasons" => CrmCatalogKind.LostReason,
        "customer-needs" => CrmCatalogKind.CustomerNeed,
        _ => throw new ArgumentException("Catalog kind must be opportunity-types, lost-reasons or customer-needs.")
    };
}

public sealed record CrmSettingsRequest(
    long ExpectedVersion, long? DefaultPipelineDefinitionId, string OpportunityCreationMode,
    long? DefaultOpportunityTypeId, bool RequireLostReason, bool RequireWonLine, string DefaultAssignmentMode,
    string AssignmentPolicy, string? DefaultPrincipalIssuer, string? DefaultPrincipalSubject,
    long? DefaultTeamId, long? DefaultTerritoryId);

public sealed record CreatePipelineDraftRequest(long? PipelineDefinitionId, string Name, long ExpectedRowVersion, int ExpectedLatestVersionNumber,
    IReadOnlyList<PipelineStageInput> Stages, bool EnforceAllowedTransitions, IReadOnlyList<PipelineTransitionInput> AllowedTransitions);
public sealed record PublishPipelineRequest(long ExpectedPipelineRowVersion);
public sealed record SetPipelineLifecycleRequest(long ExpectedRowVersion, bool IsActive, bool Archive, bool Restore = false);
public sealed record CrmCatalogRequest(long ExpectedVersion, string Name, string? Key, string? Category, decimal AveragePrice, string Status);
