using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Collaboration.Application;
using Contracts;
using Host.Authentication;

namespace Host.Endpoints;

/// <summary>The calendar HTTP surface, exactly as `docs/plans/collaboration-calendar/api-contract.md` freezes it. Request
/// bodies are read here (not bound by the framework) so every malformed input — bad JSON, a wrong token type, a timed
/// value without an offset, a missing Idempotency-Key — becomes the same 400 `validation_error` problem the rest of the API
/// uses, and never a framework-shaped 400.</summary>
public static partial class CalendarEndpoints
{
    private const int MaxBodyBytes = 64 * 1024;

    // The Web defaults read numbers from quoted strings; `"expectedVersion": "3"` is malformed, not a 3.
    private static readonly JsonSerializerOptions BodyOptions = new(JsonSerializerDefaults.Web) { NumberHandling = JsonNumberHandling.Strict };

    public static void MapCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/calendar/entries").RequireAuthorization();

        group.MapGet("/", async (HttpContext httpContext, ListCalendarEntriesHandler handler, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var query = new ListCalendarEntriesQuery(
                actor.TenantId, actor.Principal,
                RequiredInstant(httpContext.Request.Query["from"], "from"),
                RequiredInstant(httpContext.Request.Query["to"], "to"),
                actor.CorrelationId);
            var entries = await handler.HandleAsync(query, cancellationToken);
            return Results.Ok(new CalendarEntryListResponse(entries.Select(CalendarEntryResponse.From).ToList()));
        });

        group.MapGet("/{id:long}", async (long id, HttpContext httpContext, GetCalendarEntryHandler handler, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var entry = await handler.HandleAsync(new GetCalendarEntryQuery(actor.TenantId, id, actor.Principal, actor.CorrelationId), cancellationToken)
                ?? throw new CalendarEntryNotFoundException(id);
            return Results.Ok(CalendarEntryResponse.From(entry));
        });

        group.MapPost("/", async (HttpContext httpContext, CreateCalendarEntryHandler handler, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var body = await ReadBodyAsync<CalendarEntryRequest>(httpContext.Request, cancellationToken);
            var idempotencyKey = RequiredIdempotencyKey(httpContext.Request);
            var fields = body.ToFields(actor.TenantId);
            var command = new CreateCalendarEntryCommand(
                actor.TenantId, actor.Principal, fields.Title, fields.Notes, fields.Color, fields.AllDay, fields.StartAt, fields.EndAt,
                fields.StartDate, fields.EndDate, fields.Link, idempotencyKey, actor.CorrelationId);
            var result = await handler.HandleAsync(command, cancellationToken);
            return Results.Created($"/calendar/entries/{result.Id}", result);
        });

        group.MapPut("/{id:long}", async (long id, HttpContext httpContext, UpdateCalendarEntryHandler handler, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var body = await ReadBodyAsync<CalendarEntryRequest>(httpContext.Request, cancellationToken);
            var idempotencyKey = RequiredIdempotencyKey(httpContext.Request);
            var expectedVersion = body.ExpectedVersion is >= 0 and var version
                ? version
                : throw new ArgumentException("expectedVersion is required and must be a non-negative integer.");
            var fields = body.ToFields(actor.TenantId);
            var command = new UpdateCalendarEntryCommand(
                actor.TenantId, id, actor.Principal, expectedVersion, fields.Title, fields.Notes, fields.Color, fields.AllDay,
                fields.StartAt, fields.EndAt, fields.StartDate, fields.EndDate, fields.Link, idempotencyKey, actor.CorrelationId);
            return Results.Ok(await handler.HandleAsync(command, cancellationToken));
        });

        group.MapDelete("/{id:long}", async (long id, HttpContext httpContext, DeleteCalendarEntryHandler handler, CancellationToken cancellationToken) =>
        {
            var actor = httpContext.GetActorContext();
            var idempotencyKey = RequiredIdempotencyKey(httpContext.Request);
            var expectedVersion = ParseExpectedVersion(httpContext.Request.Query["expectedVersion"]);
            await handler.HandleAsync(
                new DeleteCalendarEntryCommand(actor.TenantId, id, actor.Principal, expectedVersion, idempotencyKey, actor.CorrelationId),
                cancellationToken);
            return Results.NoContent();
        });
    }

    private static async Task<T> ReadBodyAsync<T>(HttpRequest request, CancellationToken cancellationToken) where T : class
    {
        if (request.ContentLength is > MaxBodyBytes)
            throw new ArgumentException("The request body is too large.");

        try
        {
            return await JsonSerializer.DeserializeAsync<T>(request.Body, BodyOptions, cancellationToken)
                ?? throw new ArgumentException("A JSON request body is required.");
        }
        catch (JsonException)
        {
            // Never echo the payload: it can hold the title or the notes.
            throw new ArgumentException("The request body is not valid for this operation.");
        }
    }

    private static string RequiredIdempotencyKey(HttpRequest request)
    {
        // Several values would be joined with commas into a key nobody sent, so exactly one is accepted.
        var values = request.Headers["Idempotency-Key"];
        return values is [{ } key] && !string.IsNullOrWhiteSpace(key)
            ? key
            : throw new ArgumentException("Exactly one non-blank Idempotency-Key header is required.");
    }

    private static long ParseExpectedVersion(string? value) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            ? version
            : throw new ArgumentException("expectedVersion must be a non-negative integer.");

    private static DateTimeOffset RequiredInstant(string? value, string field) =>
        ParseInstant(value, field) ?? throw new ArgumentException($"{field} is required.");

    /// <summary>Accepts only an ISO 8601 instant that carries an offset (`Z` or `±hh:mm`). The framework's own
    /// `DateTimeOffset` binding would silently read an offset-less value in the server's local zone, which would make the
    /// result depend on where the host runs.</summary>
    private static DateTimeOffset? ParseInstant(string? value, string field)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        return OffsetInstant().IsMatch(value)
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instant)
                ? instant
                : throw new ArgumentException($"{field} must be an ISO 8601 instant with an offset (Z or ±hh:mm).");
    }

    private static DateOnly? ParseDate(string? value, string field)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new ArgumentException($"{field} must be a date in yyyy-MM-dd format.");
    }

    [GeneratedRegex(@"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}(:[0-9]{2}(\.[0-9]{1,7})?)?(Z|[+-][0-9]{2}:[0-9]{2})\z", RegexOptions.CultureInvariant)]
    private static partial Regex OffsetInstant();

    private sealed record EntryFields(
        string Title, string? Notes, string Color, bool AllDay, DateTimeOffset? StartAt, DateTimeOffset? EndAt,
        DateOnly? StartDate, DateOnly? EndDate, EntityRef? Link);

    private static EntryFields ToFields(this CalendarEntryRequest body, TenantId tenantId)
    {
        var title = body.Title ?? throw new ArgumentException("title is required.");
        var color = body.Color ?? throw new ArgumentException("color is required.");
        var allDay = body.AllDay ?? throw new ArgumentException("allDay is required.");
        var startAt = ParseInstant(body.StartAt, "startAt");
        var endAt = ParseInstant(body.EndAt, "endAt");
        var startDate = ParseDate(body.StartDate, "startDate");
        var endDate = ParseDate(body.EndDate, "endDate");

        return new EntryFields(title, body.Notes, color, allDay, startAt, endAt, startDate, endDate, body.Link?.ToReference(tenantId));
    }

    /// <summary>The tenant is the caller's, never the body's. A malformed reference is a 400; whether its target exists or may
    /// be seen is decided (and answered identically for every failure) by the handler through the link directory.</summary>
    private static EntityRef ToReference(this CalendarLinkRequest link, TenantId tenantId) =>
        new(
            tenantId,
            link.BoundedContext ?? throw new ArgumentException("link.boundedContext is required."),
            link.EntityType ?? throw new ArgumentException("link.entityType is required."),
            link.Id ?? throw new ArgumentException("link.id is required."));
}

/// <summary>Create / full-replace body. `ExpectedVersion` is required on PUT only; the tenant never comes from here.</summary>
public sealed record CalendarEntryRequest(
    string? Title, string? Notes, string? Color, bool? AllDay, string? StartAt, string? EndAt,
    string? StartDate, string? EndDate, CalendarLinkRequest? Link, long? ExpectedVersion);

public sealed record CalendarLinkRequest(string? BoundedContext, string? EntityType, long? Id);

public sealed record CalendarEntryListResponse(IReadOnlyList<CalendarEntryResponse> Items);

public sealed record CalendarEntryResponse(
    long Id, long RowVersion, string Title, string? Notes, string Color, bool AllDay, DateTimeOffset? StartAt,
    DateTimeOffset? EndAt, DateOnly? StartDate, DateOnly? EndDate, CalendarLinkResponse? Link)
{
    public static CalendarEntryResponse From(CalendarEntryDto entry) =>
        new(entry.Id, entry.RowVersion, entry.Title, entry.Notes, entry.Color, entry.AllDay, entry.StartAt, entry.EndAt,
            entry.StartDate, entry.EndDate, CalendarLinkResponse.From(entry.Link));
}

public sealed record CalendarLinkResponse(
    CalendarLinkRefResponse Ref, string State,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Label = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Subtitle = null)
{
    // `label`/`subtitle` are only ever set for an accessible link (and then omitted from the JSON when null).
    public static CalendarLinkResponse? From(CalendarEntryLinkDto? link) =>
        link is null
            ? null
            : new CalendarLinkResponse(
                new CalendarLinkRefResponse(link.Ref.BoundedContext, link.Ref.EntityType, link.Ref.Id),
                link.Accessible ? "accessible" : "unavailable",
                link.Accessible ? link.Label : null,
                link.Accessible ? link.Subtitle : null);
}

public sealed record CalendarLinkRefResponse(string BoundedContext, string EntityType, long Id);
