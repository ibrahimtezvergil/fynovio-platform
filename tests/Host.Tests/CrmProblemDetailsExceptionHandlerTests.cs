using System.Text.Json;
using Contracts;
using CRM.Application;
using Host.Endpoints;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Host.Tests;

/// <summary>Pure unit tests (no Postgres, no WebApplicationFactory) for the 2026-09-19
/// authorization-delta's external mapping: a `DenialStage.Record` denial must produce a
/// byte-identical response shape to a genuinely missing opportunity — same status, same
/// `type`, same `title` text — never the raw "was denied" message, which would leak that
/// the record exists even at the same status code. A `DenialStage.Coarse` denial stays a
/// visible 403. See docs/architecture-analysis/2026-09-19-crm-phase2-authorization-delta-and-entry-stage-resolution.md.</summary>
public sealed class CrmProblemDetailsExceptionHandlerTests
{
    private readonly CrmProblemDetailsExceptionHandler _handler = new();

    [Fact]
    public async Task Record_level_denial_and_genuinely_missing_opportunity_produce_identical_responses()
    {
        var recordDenied = await HandleAsync(new OpportunityAuthorizationDeniedException(
            "crm.opportunity.win", "no_matching_grant", AuthorizationDenialStage.Record, opportunityId: 42));
        var genuinelyMissing = await HandleAsync(new OpportunityNotFoundException(42));

        Assert.Equal(genuinelyMissing.Status, recordDenied.Status);
        Assert.Equal(StatusCodes.Status404NotFound, recordDenied.Status);
        Assert.Equal(genuinelyMissing.Type, recordDenied.Type);
        Assert.Equal("not_found", recordDenied.Type);
        Assert.Equal(genuinelyMissing.Title, recordDenied.Title);
        Assert.DoesNotContain("denied", recordDenied.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Coarse_denial_stays_visibly_forbidden()
    {
        var response = await HandleAsync(new OpportunityAuthorizationDeniedException(
            "crm.opportunity.win", "no_matching_grant", AuthorizationDenialStage.Coarse, opportunityId: 42));

        Assert.Equal(StatusCodes.Status403Forbidden, response.Status);
        Assert.Equal("forbidden", response.Type);
    }

    /// <summary>Same coarse behavior for CreateOpportunity's shape, which never carries
    /// an OpportunityId (no resource exists yet — DenialStage can only ever be Coarse
    /// there).</summary>
    [Fact]
    public async Task Coarse_denial_with_no_opportunity_id_stays_forbidden()
    {
        var response = await HandleAsync(new OpportunityAuthorizationDeniedException(
            "crm.opportunity.create", "no_matching_grant", AuthorizationDenialStage.Coarse));

        Assert.Equal(StatusCodes.Status403Forbidden, response.Status);
        Assert.Equal("forbidden", response.Type);
    }

    private async Task<(int Status, string? Type, string? Title)> HandleAsync(Exception exception)
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);
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
