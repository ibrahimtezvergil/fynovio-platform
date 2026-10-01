using System.Security.Claims;
using System.Text.Json;
using Contracts;
using CRM.Application;
using Host.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Host.Endpoints;

public static class OpportunityEndpoints
{
    public static void MapOpportunityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/opportunities").RequireAuthorization();

        group.MapPost("/", async (
            CreateOpportunityRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            CreateOpportunityHandler handler, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var command = new CreateOpportunityCommand(
                actor.TenantId, new PartyRef(actor.TenantId, request.PartyId), actor.Principal,
                request.Currency, request.EstimatedAmount, idempotencyKey, actor.CorrelationId)
            { CallerPrincipal = actor.Principal, CustomFields = request.CustomFields };
            var result = await handler.HandleAsync(command, cancellationToken);
            return Results.Created($"/opportunities/{result.OpportunityId}", result);
        });

        group.MapPost("/{id:long}/lines", async (
            long id, AddOpportunityLineRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            AddOpportunityLineHandler handler, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var command = new AddOpportunityLineCommand(
                actor.TenantId, id, actor.Principal, request.ExpectedVersion,
                new EntityRef(actor.TenantId, "masterdata", "product", request.ProductId),
                request.Quantity, request.UnitPrice, request.IsOptional, request.SortOrder,
                idempotencyKey, actor.CorrelationId);
            return Results.Ok(await handler.HandleAsync(command, cancellationToken));
        });

        group.MapPost("/{id:long}/lines/{lineId:long}/cancel", async (
            long id, long lineId, CancelOpportunityLineRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            CancelOpportunityLineHandler handler, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var command = new CancelOpportunityLineCommand(
                actor.TenantId, id, lineId, actor.Principal, request.ExpectedVersion, request.CancelReason,
                idempotencyKey, actor.CorrelationId);
            return Results.Ok(await handler.HandleAsync(command, cancellationToken));
        });

        group.MapPost("/{id:long}/open", async (
            long id, OpenOpportunityRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            OpenOpportunityHandler handler, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var command = new OpenOpportunityCommand(
                actor.TenantId, id, actor.Principal, request.ExpectedVersion, request.ExpiryDate, idempotencyKey, actor.CorrelationId);
            return Results.Ok(await handler.HandleAsync(command, cancellationToken));
        });

        group.MapPost("/{id:long}/stage", async (
            long id, ChangePipelineStageRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            ChangePipelineStageHandler handler, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var command = new ChangePipelineStageCommand(
                actor.TenantId, id, actor.Principal, request.ExpectedVersion, request.TargetStageId, idempotencyKey, actor.CorrelationId);
            return Results.Ok(await handler.HandleAsync(command, cancellationToken));
        });

        group.MapPost("/{id:long}/move-pipeline", async (
            long id, MoveOpportunityToPipelineRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            MoveOpportunityToPipelineHandler handler, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var command = new MoveOpportunityToPipelineCommand(
                actor.TenantId, id, actor.Principal, request.ExpectedVersion, request.TargetPipelineDefinitionVersionId, request.TargetStageId, idempotencyKey, actor.CorrelationId);
            return Results.Ok(await handler.HandleAsync(command, cancellationToken));
        });

        group.MapPost("/{id:long}/win", async (
            long id, WinOpportunityRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            WinOpportunityHandler handler, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var command = new WinOpportunityCommand(actor.TenantId, id, actor.Principal, request.ExpectedVersion, idempotencyKey, actor.CorrelationId);
            return Results.Ok(await handler.HandleAsync(command, cancellationToken));
        });

        group.MapPost("/{id:long}/lose", async (
            long id, LoseOpportunityRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            LoseOpportunityHandler handler, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var command = new LoseOpportunityCommand(
                actor.TenantId, id, actor.Principal, request.ExpectedVersion, request.LostReason, idempotencyKey, actor.CorrelationId);
            return Results.Ok(await handler.HandleAsync(command, cancellationToken));
        });

        group.MapPut("/{id:long}/custom-fields", async (
            long id, UpdateOpportunityCustomFieldsRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            UpdateOpportunityCustomFieldsHandler handler, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var command = new UpdateOpportunityCustomFieldsCommand(
                actor.TenantId, id, actor.Principal, request.ExpectedVersion, request.CustomFields, idempotencyKey, actor.CorrelationId);
            return Results.Ok(await handler.HandleAsync(command, cancellationToken));
        });

        group.MapPost("/{id:long}/reassign", async (
            long id, ReassignOpportunityRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            ReassignOpportunityHandler handler, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var newOwner = new PrincipalRef(request.NewPrincipalIssuer, request.NewPrincipalSubject);
            var command = new ReassignOpportunityCommand(
                actor.TenantId, id, actor.Principal, request.ExpectedVersion, newOwner, idempotencyKey, actor.CorrelationId);
            return Results.Ok(await handler.HandleAsync(command, cancellationToken));
        });

        group.MapPost("/{id:long}/archive", async (
            long id, OpportunityArchiveRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            SetOpportunityArchiveHandler handler,
            HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var command = new SetOpportunityArchiveCommand(actor.TenantId, id, actor.Principal, request.ExpectedVersion,
                Archive: true, request.ConfirmOpenOpportunity, RestoreStageId: null, idempotencyKey, actor.CorrelationId);
            return Results.Ok(await handler.HandleAsync(command, cancellationToken));
        });

        group.MapPost("/{id:long}/restore", async (
            long id, OpportunityRestoreRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            SetOpportunityArchiveHandler handler,
            HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var command = new SetOpportunityArchiveCommand(actor.TenantId, id, actor.Principal, request.ExpectedVersion,
                Archive: false, ConfirmOpenArchive: false, request.StageId, idempotencyKey, actor.CorrelationId);
            return Results.Ok(await handler.HandleAsync(command, cancellationToken));
        });

        group.MapGet("/{id:long}", async (
            long id, GetOpportunityHandler handler, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var dto = await handler.HandleAsync(new GetOpportunityQuery(actor.TenantId, id, actor.Principal, actor.CorrelationId), cancellationToken);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        });

        group.MapGet("/{id:long}/activity", async (
            long id, GetOpportunityActivityHandler handler, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var entries = await handler.HandleAsync(new GetOpportunityActivityQuery(actor.TenantId, id, actor.Principal, actor.CorrelationId), cancellationToken);
            return entries is null ? Results.NotFound() : Results.Ok(entries);
        });

        group.MapGet("/", async (
            [AsParameters] ListOpportunitiesRequest request, ListOpportunitiesHandler handler, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            if (request.Skip < 0 || request.Take <= 0 || request.Take > 1000)
                throw new ArgumentException("Skip must be >= 0, Take must be between 1 and 1000.");
            var query = new ListOpportunitiesQuery(actor.TenantId, actor.Principal, actor.CorrelationId, request.Status, request.Skip, request.Take, request.ArchivedOnly,
                ParseCustomFieldFilters(request.Cf));
            return Results.Ok(await handler.HandleAsync(query, cancellationToken));
        });

        group.MapGet("/{id:long}/assignable-principals", async (
            long id, string? search, int? take, ListAssignablePrincipalsHandler handler, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var query = new ListAssignablePrincipalsQuery(actor.TenantId, id, actor.Principal, search, take ?? ListAssignablePrincipalsHandler.DefaultTake, actor.CorrelationId);
            return Results.Ok(await handler.HandleAsync(query, cancellationToken));
        });

        group.MapGet("/{id:long}/actions", async (
            long id, GetOpportunityAvailableActionsHandler handler,
            HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var dto = await handler.HandleAsync(new GetOpportunityAvailableActionsQuery(actor.TenantId, id, actor.Principal, actor.CorrelationId), cancellationToken);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        });

        app.MapGroup("/crm/references").RequireAuthorization().MapGet("/parties", async (
            string? search, string? ids, int? take, SearchPartyReferencesHandler handler, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var query = new SearchPartyReferencesQuery(
                actor.TenantId, actor.Principal, search, ParseIds(ids), take ?? SearchPartyReferencesHandler.DefaultTake, actor.CorrelationId);
            return Results.Ok(await handler.HandleAsync(query, cancellationToken));
        });

        app.MapGroup("/crm/references").RequireAuthorization().MapPost("/parties", async (
            CreatePartyReferenceRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            CreatePartyReferenceHandler handler, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<PartyType>(request.PartyType, ignoreCase: true, out var partyType) || !Enum.IsDefined(partyType))
                throw new ArgumentException("partyType must be 'Person' or 'Organization'.");

            var actor = httpContext.GetActorContext();
            var command = new CreatePartyReferenceCommand(
                actor.TenantId, actor.Principal, partyType, request.Name ?? string.Empty, request.Surname, request.Phone, request.Email,
                idempotencyKey, actor.CorrelationId);
            var result = await handler.HandleAsync(command, cancellationToken);
            return Results.Created($"/crm/references/parties?ids={result.Id}", result);
        });

        app.MapGroup("/pipelines").RequireAuthorization().MapGet("/{versionId:long}/stages", async (
            long versionId, GetPipelineStagesHandler handler, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            return Results.Ok(await handler.HandleAsync(new GetPipelineStagesQuery(actor.TenantId, versionId, actor.Principal, actor.CorrelationId), cancellationToken));
        });
        app.MapGroup("/pipelines").RequireAuthorization().MapGet("/default/stages", async (
            GetDefaultPipelineStagesHandler handler, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            return Results.Ok(await handler.HandleAsync(new GetDefaultPipelineStagesQuery(actor.TenantId, actor.Principal, actor.CorrelationId), cancellationToken));
        });
    }

    /// <summary>`ids=1,2,3` → [1,2,3]; a malformed list is a 400 (ArgumentException → validation_error), not a silent partial answer.</summary>
    private static IReadOnlyCollection<long>? ParseIds(string? ids)
    {
        if (string.IsNullOrWhiteSpace(ids))
            return null;

        var parts = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var parsed = new List<long>(parts.Length);
        foreach (var part in parts)
        {
            if (!long.TryParse(part, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id))
                throw new ArgumentException("ids must be a comma-separated list of positive identifiers.");
            parsed.Add(id);
        }

        return parsed;
    }

    /// <summary>`cf=region:north&amp;cf=vip:true` → {region: north, vip: true}; a malformed or repeated key is a 400.
    /// Whether the key and value fit a definition is the handler's check (422 custom_field_invalid).</summary>
    private static IReadOnlyDictionary<string, string>? ParseCustomFieldFilters(string[]? filters)
    {
        if (filters is null || filters.Length == 0)
            return null;

        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var filter in filters)
        {
            var separator = filter.IndexOf(':');
            if (separator <= 0 || separator == filter.Length - 1)
                throw new ArgumentException("cf must be key:value.");
            if (!parsed.TryAdd(filter[..separator], filter[(separator + 1)..]))
                throw new ArgumentException("cf names each field at most once.");
        }

        return parsed;
    }
}

public sealed record CreateOpportunityRequest(long PartyId, string Currency, decimal EstimatedAmount, JsonElement? CustomFields = null);
public sealed record UpdateOpportunityCustomFieldsRequest(long ExpectedVersion, JsonElement? CustomFields);

public sealed record CreatePartyReferenceRequest(string? PartyType, string? Name, string? Surname, string? Phone, string? Email);
public sealed record AddOpportunityLineRequest(long ExpectedVersion, long ProductId, int Quantity, decimal UnitPrice, bool IsOptional, int SortOrder);
public sealed record CancelOpportunityLineRequest(long ExpectedVersion, string CancelReason);
public sealed record OpenOpportunityRequest(long ExpectedVersion, DateTimeOffset ExpiryDate);
public sealed record ChangePipelineStageRequest(long ExpectedVersion, long TargetStageId);
public sealed record MoveOpportunityToPipelineRequest(long ExpectedVersion, long TargetPipelineDefinitionVersionId, long TargetStageId);
public sealed record WinOpportunityRequest(long ExpectedVersion);
public sealed record LoseOpportunityRequest(long ExpectedVersion, string LostReason);
public sealed record ReassignOpportunityRequest(long ExpectedVersion, string NewPrincipalIssuer, string NewPrincipalSubject);
public sealed record OpportunityArchiveRequest(long ExpectedVersion, bool ConfirmOpenOpportunity = false);
public sealed record OpportunityRestoreRequest(long ExpectedVersion, long? StageId = null);
/// <summary>`cf` repeats as `key:value` — an equality filter on a select, multi-select or boolean custom field.</summary>
public sealed record ListOpportunitiesRequest(CRM.Domain.OpportunityStatus? Status, int Skip = 0, int Take = 50, bool ArchivedOnly = false, string[]? Cf = null);
