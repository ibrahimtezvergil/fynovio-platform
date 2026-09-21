using System.Text.Json;
using Collaboration.Application;
using Contracts;
using Host.Endpoints;
using Xunit;

namespace Host.Tests;

/// <summary>The response contract's shape rules that need no running host.</summary>
public sealed class CalendarEntryResponseTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static CalendarEntryDto Dto(CalendarEntryLinkDto? link) =>
        new(7, 3, "Call", null, "#3b82f6", false,
            new DateTimeOffset(2026, 9, 21, 6, 0, 0, TimeSpan.Zero), null, null, null, link);

    private static readonly EntityRef Opportunity = new(new TenantId(1), "crm", "opportunity", 17);

    [Fact]
    public void An_accessible_link_carries_its_label_and_subtitle()
    {
        var response = CalendarEntryResponse.From(Dto(new CalendarEntryLinkDto(Opportunity, true, "OPP-17 — Acme", "Proposal")));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, Web));
        var link = json.RootElement.GetProperty("link");
        Assert.Equal("accessible", link.GetProperty("state").GetString());
        Assert.Equal("OPP-17 — Acme", link.GetProperty("label").GetString());
        Assert.Equal("Proposal", link.GetProperty("subtitle").GetString());
        Assert.Equal(["label", "ref", "state", "subtitle"], link.EnumerateObject().Select(p => p.Name).Order().ToArray());
    }

    [Fact]
    public void An_accessible_link_without_a_subtitle_omits_the_key_rather_than_writing_null()
    {
        var response = CalendarEntryResponse.From(Dto(new CalendarEntryLinkDto(Opportunity, true, "OPP-17 — Acme")));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, Web));
        Assert.Equal(["label", "ref", "state"], json.RootElement.GetProperty("link").EnumerateObject().Select(p => p.Name).Order().ToArray());
    }

    [Fact]
    public void An_unavailable_link_never_carries_a_label_or_subtitle_even_if_the_dto_holds_one()
    {
        var response = CalendarEntryResponse.From(Dto(new CalendarEntryLinkDto(Opportunity, false, "LEAK", "LEAK")));

        var raw = JsonSerializer.Serialize(response, Web);
        Assert.DoesNotContain("LEAK", raw);
    }

    [Fact]
    public void An_unavailable_link_is_reported_with_its_reference_and_no_label_or_subtitle_keys()
    {
        var response = CalendarEntryResponse.From(Dto(new CalendarEntryLinkDto(Opportunity, false)));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, Web));
        var link = json.RootElement.GetProperty("link");
        Assert.Equal("unavailable", link.GetProperty("state").GetString());
        Assert.Equal("crm", link.GetProperty("ref").GetProperty("boundedContext").GetString());
        Assert.Equal("opportunity", link.GetProperty("ref").GetProperty("entityType").GetString());
        Assert.Equal(17, link.GetProperty("ref").GetProperty("id").GetInt64());
        Assert.Equal(["ref", "state"], link.EnumerateObject().Select(p => p.Name).Order().ToArray());
    }

    [Fact]
    public void Absent_optional_values_are_written_as_null_not_omitted_and_instants_use_a_zero_offset()
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(CalendarEntryResponse.From(Dto(null)), Web));

        var root = json.RootElement;
        Assert.Equal(
            ["allDay", "color", "endAt", "endDate", "id", "link", "notes", "rowVersion", "startAt", "startDate", "title"],
            root.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("link").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("notes").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("endAt").ValueKind);
        Assert.Equal("2026-09-21T06:00:00+00:00", root.GetProperty("startAt").GetString());
    }

    [Fact]
    public void All_day_dates_are_written_as_timezone_free_iso_dates()
    {
        var dto = new CalendarEntryDto(1, 1, "Offsite", null, "#10b981", true, null, null, new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 22), null);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(CalendarEntryResponse.From(dto), Web));

        Assert.Equal("2026-09-21", json.RootElement.GetProperty("startDate").GetString());
        Assert.Equal("2026-09-22", json.RootElement.GetProperty("endDate").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("startAt").ValueKind);
    }
}
