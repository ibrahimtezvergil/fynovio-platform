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

    private static CalendarEntryDto Dto(EntityRef? link) =>
        new(7, 3, "Call", null, "#3b82f6", false,
            new DateTimeOffset(2026, 9, 21, 6, 0, 0, TimeSpan.Zero), null, null, null, link);

    [Fact]
    public void A_stored_link_is_reported_unavailable_with_its_reference_and_no_label_or_subtitle_keys()
    {
        var response = CalendarEntryResponse.From(Dto(new EntityRef(new TenantId(1), "crm", "opportunity", 17)));

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
