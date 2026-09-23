using System.Text.Json;
using System.Text.Json.Serialization;
using Access.Application;
using Access.Application.Authentication;
using Contracts;
using Host.Authentication;
using TenantLifecycle.Application;
using TenantLifecycle.Domain;

namespace Host.Endpoints;

public static class CompanySettingsEndpoints
{
    private const int MaxBodyBytes = 16 * 1024;
    private const int MaxEmailLength = 255;
    private const int MaxDisplayNameLength = 200;
    private static readonly JsonSerializerOptions BodyOptions = new(JsonSerializerDefaults.Web) { NumberHandling = JsonNumberHandling.Strict };

    public static void MapCompanySettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/company/settings").RequireAuthorization();
        group.MapGet("", async (HttpContext context, GetCompanySettingsHandler handler, CancellationToken cancellationToken) =>
        {
            var actor = context.GetActorContext();
            return Results.Ok(await handler.HandleAsync(new GetCompanySettingsQuery(actor.TenantId, actor.Principal, actor.CorrelationId), cancellationToken));
        });
        group.MapPut("", async (HttpContext context, UpdateCompanySettingsHandler handler, CancellationToken cancellationToken) =>
        {
            var body = await ReadBodyAsync(context.Request, cancellationToken);
            var actor = context.GetActorContext();
            var result = await handler.HandleAsync(new UpdateCompanySettingsCommand(
                actor.TenantId, actor.Principal, body.ToDetails(), body.ExpectedVersion,
                RequiredIdempotencyKey(context.Request), actor.CorrelationId), cancellationToken);
            return Results.Ok(result);
        });
        group.MapGet("/access", async (HttpContext context, GetTenantAccessOverviewHandler handler, CancellationToken cancellationToken) =>
        {
            var actor = context.GetActorContext();
            return Results.Ok(await handler.HandleAsync(new GetTenantAccessOverviewQuery(actor.TenantId, actor.Principal, actor.CorrelationId), cancellationToken));
        });
        group.MapPost("/invitations", async (HttpContext context, CreateInvitationHandler handler, ClientFingerprint fingerprint, CancellationToken cancellationToken) =>
        {
            var body = await ReadInvitationBodyAsync(context.Request, cancellationToken);
            if (string.IsNullOrWhiteSpace(body.Email) || body.Email.Length > MaxEmailLength || body.DisplayName?.Length > MaxDisplayNameLength)
                throw new ArgumentException("A valid e-mail address and display name are required.");
            if (body.RoleKey is not null && (string.IsNullOrWhiteSpace(body.RoleKey) || body.RoleKey.Length > 120))
                throw new ArgumentException("A valid role key is required.");

            var actor = context.GetActorContext();
            var result = await handler.HandleAsync(
                new CreateInvitationCommand(actor, body.Email, body.DisplayName, body.Locale, fingerprint.HashIp(context),
                    RequiredIdempotencyKey(context.Request), body.RoleKey), cancellationToken);
            return result.Status switch
            {
                CreateInvitationStatus.Accepted => Results.Accepted(value: new { status = "accepted" }),
                CreateInvitationStatus.Forbidden => Results.Forbid(),
                _ => Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Email is not valid."] })
            };
        });
        group.MapDelete("/invitations/{invitationId:guid}", async (Guid invitationId, HttpContext context,
            CancelInvitationHandler handler, CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(context.GetActorContext(), invitationId,
                RequiredIdempotencyKey(context.Request), cancellationToken);
            return Results.NoContent();
        });
        group.MapPost("/role-assignments", async (HttpContext context, GrantRoleAssignmentHandler handler, CancellationToken cancellationToken) =>
        {
            var body = await ReadRoleAssignmentBodyAsync(context.Request, cancellationToken);
            var actor = context.GetActorContext();
            var principal = new PrincipalRef(body.PrincipalIssuer!, body.PrincipalSubject!);
            var result = await handler.HandleAsync(new GrantRoleAssignmentCommand(
                actor.TenantId, actor.Principal, principal, body.RoleKey!,
                body.Reason, actor.CorrelationId, RequiredIdempotencyKey(context.Request)), cancellationToken);
            return Results.Ok(result);
        });
        group.MapDelete("/role-assignments/{assignmentId:long}", async (long assignmentId, HttpContext context, RevokeRoleAssignmentHandler handler, CancellationToken cancellationToken) =>
        {
            var actor = context.GetActorContext();
            var result = await handler.HandleAsync(new RevokeRoleAssignmentCommand(
                actor.TenantId, actor.Principal, assignmentId, null, actor.CorrelationId, RequiredIdempotencyKey(context.Request)), cancellationToken);
            return Results.Ok(result);
        });
        group.MapPost("/roles", async (HttpContext context, ManageTenantRoleHandler handler, CancellationToken cancellationToken) =>
        {
            var body = await ReadRoleDefinitionBodyAsync(context.Request, cancellationToken);
            var actor = context.GetActorContext();
            var result = await handler.CreateAsync(new CreateTenantRoleCommand(actor.TenantId, actor.Principal, body.Name!,
                body.ActionKeys!, body.ExpectedRevision, actor.CorrelationId, RequiredIdempotencyKey(context.Request)), cancellationToken);
            return Results.Ok(result);
        });
        group.MapPut("/roles/{roleKey}", async (string roleKey, HttpContext context, ManageTenantRoleHandler handler, CancellationToken cancellationToken) =>
        {
            var body = await ReadRoleDefinitionBodyAsync(context.Request, cancellationToken);
            var actor = context.GetActorContext();
            var result = await handler.UpdateAsync(new UpdateTenantRoleCommand(actor.TenantId, actor.Principal, roleKey, body.Name!,
                body.ActionKeys!, body.ExpectedRevision, actor.CorrelationId, RequiredIdempotencyKey(context.Request)), cancellationToken);
            return Results.Ok(result);
        });
    }

    private static async Task<CompanySettingsRequest> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength is > MaxBodyBytes)
            throw new ArgumentException("The request body is too large.");
        try
        {
            await using var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length > MaxBodyBytes)
                throw new ArgumentException("The request body is too large.");
            return JsonSerializer.Deserialize<CompanySettingsRequest>(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)), BodyOptions)
                ?? throw new ArgumentException("A JSON request body is required.");
        }
        catch (JsonException)
        {
            throw new ArgumentException("The request body is not valid for this operation.");
        }
    }

    private static async Task<InvitationRequest> ReadInvitationBodyAsync(HttpRequest request, CancellationToken cancellationToken) =>
        await ReadJsonAsync<InvitationRequest>(request, cancellationToken);

    private static async Task<RoleAssignmentRequest> ReadRoleAssignmentBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var body = await ReadJsonAsync<RoleAssignmentRequest>(request, cancellationToken);
        if (string.IsNullOrWhiteSpace(body.PrincipalIssuer) || string.IsNullOrWhiteSpace(body.PrincipalSubject) || string.IsNullOrWhiteSpace(body.RoleKey))
            throw new ArgumentException("Principal and role are required.");
        return body;
    }

    private static async Task<RoleDefinitionRequest> ReadRoleDefinitionBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var body = await ReadJsonAsync<RoleDefinitionRequest>(request, cancellationToken);
        if (string.IsNullOrWhiteSpace(body.Name) || body.Name.Length > 120 || body.ActionKeys is not { Count: > 0 and <= 100 }
            || body.ActionKeys.Any(string.IsNullOrWhiteSpace) || body.ExpectedRevision < 0)
            throw new ArgumentException("A role name, permissions and access revision are required.");
        return body;
    }

    private static async Task<T> ReadJsonAsync<T>(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength is > MaxBodyBytes)
            throw new ArgumentException("The request body is too large.");
        try
        {
            await using var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length > MaxBodyBytes)
                throw new ArgumentException("The request body is too large.");
            return JsonSerializer.Deserialize<T>(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)), BodyOptions)
                ?? throw new ArgumentException("A JSON request body is required.");
        }
        catch (JsonException)
        {
            throw new ArgumentException("The request body is not valid for this operation.");
        }
    }

    private static string RequiredIdempotencyKey(HttpRequest request)
    {
        var values = request.Headers["Idempotency-Key"];
        return values is [{ } key] && !string.IsNullOrWhiteSpace(key)
            ? key
            : throw new ArgumentException("Exactly one non-blank Idempotency-Key header is required.");
    }

    private static TenantProfileDetails ToDetails(this CompanySettingsRequest request) => new(
        request.DisplayName!, request.LegalName, request.TaxNumber, request.TaxOffice, request.Email,
        request.Phone, request.Address, request.Timezone!, request.CurrencyCode!);
}

public sealed record CompanySettingsRequest(
    [property: JsonRequired] string? DisplayName,
    [property: JsonRequired] string? LegalName,
    [property: JsonRequired] string? TaxNumber,
    [property: JsonRequired] string? TaxOffice,
    [property: JsonRequired] string? Email,
    [property: JsonRequired] string? Phone,
    [property: JsonRequired] string? Address,
    [property: JsonRequired] string? Timezone,
    [property: JsonRequired] string? CurrencyCode,
    [property: JsonRequired] long ExpectedVersion);

public sealed record InvitationRequest(string? Email, string? DisplayName, string? Locale, string? RoleKey);

public sealed record RoleAssignmentRequest(string? PrincipalIssuer, string? PrincipalSubject, string? RoleKey, string? Reason);

public sealed record RoleDefinitionRequest(string? Name, IReadOnlyList<string>? ActionKeys, long ExpectedRevision);
