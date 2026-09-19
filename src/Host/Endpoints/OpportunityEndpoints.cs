using System.Security.Claims;
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
                request.Currency, request.EstimatedAmount, idempotencyKey, actor.CorrelationId);
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

        group.MapGet("/{id:long}", async (
            long id, GetOpportunityHandler handler, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var dto = await handler.HandleAsync(new GetOpportunityQuery(actor.TenantId, id, actor.Principal, actor.CorrelationId), cancellationToken);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        });

        group.MapGet("/", async (
            [AsParameters] ListOpportunitiesRequest request, ListOpportunitiesHandler handler, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var query = new ListOpportunitiesQuery(actor.TenantId, actor.Principal, actor.CorrelationId, request.Status, request.Skip, request.Take);
            return Results.Ok(await handler.HandleAsync(query, cancellationToken));
        });

        group.MapGet("/{id:long}/actions", async (
            long id, GetOpportunityAvailableActionsHandler handler, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var dto = await handler.HandleAsync(new GetOpportunityAvailableActionsQuery(actor.TenantId, id, actor.Principal, actor.CorrelationId), cancellationToken);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        });

        app.MapGroup("/pipelines").RequireAuthorization().MapGet("/{versionId:long}/stages", async (
            long versionId, GetPipelineStagesHandler handler, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            return Results.Ok(await handler.HandleAsync(new GetPipelineStagesQuery(actor.TenantId, versionId, actor.CorrelationId), cancellationToken));
        });
    }
}

public sealed record CreateOpportunityRequest(long PartyId, string Currency, decimal EstimatedAmount);
public sealed record AddOpportunityLineRequest(long ExpectedVersion, long ProductId, int Quantity, decimal UnitPrice, bool IsOptional, int SortOrder);
public sealed record CancelOpportunityLineRequest(long ExpectedVersion, string CancelReason);
public sealed record OpenOpportunityRequest(long ExpectedVersion, DateTimeOffset ExpiryDate);
public sealed record ChangePipelineStageRequest(long ExpectedVersion, long TargetStageId);
public sealed record WinOpportunityRequest(long ExpectedVersion);
public sealed record LoseOpportunityRequest(long ExpectedVersion, string LostReason);
public sealed record ReassignOpportunityRequest(long ExpectedVersion, string NewPrincipalIssuer, string NewPrincipalSubject);
public sealed record ListOpportunitiesRequest(CRM.Domain.OpportunityStatus? Status, int Skip = 0, int Take = 50);
