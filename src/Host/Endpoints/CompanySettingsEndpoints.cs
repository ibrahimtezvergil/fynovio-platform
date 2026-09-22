using System.Text.Json;
using System.Text.Json.Serialization;
using Host.Authentication;
using TenantLifecycle.Application;
using TenantLifecycle.Domain;

namespace Host.Endpoints;

public static class CompanySettingsEndpoints
{
    private const int MaxBodyBytes = 16 * 1024;
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
