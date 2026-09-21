using System.Text.Json;
using Collaboration.Application;
using Contracts;
using CRM.Application;
using Host.Endpoints;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Host.Tests;

/// <summary>Pure unit tests for the Collaboration error contract, including that it coexists with the CRM handler:
/// each handler only claims its own module's exception types and passes on everything else.</summary>
public sealed class CollaborationProblemDetailsExceptionHandlerTests
{
    private readonly CollaborationProblemDetailsExceptionHandler _handler = new();
    private readonly CrmProblemDetailsExceptionHandler _crmHandler = new();

    [Fact]
    public async Task Record_level_denial_and_genuinely_missing_entry_produce_identical_responses()
    {
        var recordDenied = await HandleAsync(_handler, new CalendarEntryAuthorizationDeniedException(
            "collaboration.calendar_entry.update", "not_owner", AuthorizationDenialStage.Record, entryId: 42));
        var genuinelyMissing = await HandleAsync(_handler, new CalendarEntryNotFoundException(42));

        Assert.Equal(StatusCodes.Status404NotFound, recordDenied.Status);
        Assert.Equal(genuinelyMissing.Status, recordDenied.Status);
        Assert.Equal("not_found", recordDenied.Type);
        Assert.Equal(genuinelyMissing.Type, recordDenied.Type);
        Assert.Equal(genuinelyMissing.Title, recordDenied.Title);
        Assert.DoesNotContain("denied", recordDenied.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Coarse_denial_stays_visibly_forbidden()
    {
        var response = await HandleAsync(_handler, new CalendarEntryAuthorizationDeniedException(
            "collaboration.calendar_entry.list", "no_matching_grant", AuthorizationDenialStage.Coarse));

        Assert.Equal(StatusCodes.Status403Forbidden, response.Status);
        Assert.Equal("forbidden", response.Type);
    }

    [Fact]
    public async Task A_stale_version_maps_to_a_concurrency_conflict()
    {
        var response = await HandleAsync(_handler, new CalendarEntryConcurrencyConflictException(42, expectedVersion: 1, actualVersion: 2));

        Assert.Equal(StatusCodes.Status409Conflict, response.Status);
        Assert.Equal("concurrency_conflict", response.Type);
    }

    [Fact]
    public async Task A_reused_idempotency_key_maps_to_its_own_conflict_type()
    {
        var response = await HandleAsync(_handler, new Collaboration.Application.IdempotencyKeyReusedException("CreateCalendarEntry", "key-1"));

        Assert.Equal(StatusCodes.Status409Conflict, response.Status);
        Assert.Equal("idempotency_key_reused", response.Type);
    }

    [Fact]
    public async Task An_oversized_range_maps_to_422_range_too_large()
    {
        var response = await HandleAsync(_handler, new CalendarRangeTooLargeException("Range exceeds 100 days."));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, response.Status);
        Assert.Equal("range_too_large", response.Type);
    }

    [Fact]
    public async Task Collaboration_does_not_claim_validation_errors_or_crm_exceptions()
    {
        Assert.False(await TryHandleAsync(_handler, new ArgumentException("title is required")));
        Assert.False(await TryHandleAsync(_handler, new OpportunityNotFoundException(1)));
        Assert.False(await TryHandleAsync(_handler, new CRM.Application.IdempotencyKeyReusedException("CreateOpportunity", "key-1")));
        Assert.False(await TryHandleAsync(_handler, new InvalidOperationException("anything")));
    }

    [Fact]
    public async Task The_crm_handler_does_not_claim_collaboration_exceptions()
    {
        Assert.False(await TryHandleAsync(_crmHandler, new CalendarEntryNotFoundException(1)));
        Assert.False(await TryHandleAsync(_crmHandler, new CalendarEntryAuthorizationDeniedException("collaboration.calendar_entry.read", "x", AuthorizationDenialStage.Coarse)));
        Assert.False(await TryHandleAsync(_crmHandler, new CalendarEntryConcurrencyConflictException(1, 1, 2)));
        Assert.False(await TryHandleAsync(_crmHandler, new Collaboration.Application.IdempotencyKeyReusedException("CreateCalendarEntry", "key-1")));
        Assert.False(await TryHandleAsync(_crmHandler, new CalendarRangeTooLargeException("too many")));
    }

    [Fact]
    public async Task Validation_errors_still_reach_the_crm_handler_as_400()
    {
        var response = await HandleAsync(_crmHandler, new ArgumentException("Title must be a trimmed single line."));

        Assert.Equal(StatusCodes.Status400BadRequest, response.Status);
        Assert.Equal("validation_error", response.Type);
    }

    private static async Task<bool> TryHandleAsync(Microsoft.AspNetCore.Diagnostics.IExceptionHandler handler, Exception exception)
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        return await handler.TryHandleAsync(context, exception, CancellationToken.None);
    }

    private static async Task<(int Status, string? Type, string? Title)> HandleAsync(Microsoft.AspNetCore.Diagnostics.IExceptionHandler handler, Exception exception)
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);
        Assert.True(handled);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var root = document.RootElement;
        return (
            context.Response.StatusCode,
            root.TryGetProperty("type", out var type) ? type.GetString() : null,
            root.TryGetProperty("title", out var title) ? title.GetString() : null);
    }
}
