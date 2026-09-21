using System.Text.Json;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Collaboration.Tests.Integration;

/// <summary>The outbox row and idempotency record written with a create: shape, thin payload, privacy.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class CalendarEntryOutboxTests(PostgresFixture fixture)
{
    private const string SecretTitle = "Quarterly layoff planning";
    private const string SecretNotes = "confidential: severance figures";

    private readonly Harness _harness = new(fixture);

    private static readonly string[] ExpectedPayloadProperties =
    [
        "EntryId", "OwnerPrincipalIssuer", "OwnerPrincipalSubject", "AllDay", "StartAt", "EndAt",
        "StartDate", "EndDate", "LinkBoundedContext", "LinkEntityType", "LinkEntityId"
    ];

    [Fact]
    public async Task A_timed_create_writes_one_cloudevents_shaped_outbox_row()
    {
        var tenant = TestData.NextTenant();
        var correlationId = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 3, 10, 13, 0, 0, TimeSpan.FromHours(3));
        var link = new EntityRef(tenant, "crm", "opportunity", 42);

        var created = await _harness.CreateAsync(Commands.Timed(
            tenant, Harness.Alice, "k-ob", title: SecretTitle, notes: SecretNotes, start: start,
            end: start.AddHours(2), link: link, correlationId: correlationId));

        await using var admin = fixture.CreateAdminContext();
        var message = await admin.OutboxMessages.SingleAsync(m => m.TenantId == tenant);
        Assert.Equal("CalendarEntry", message.AggregateType);
        Assert.Equal(created.Id, message.AggregateId);
        Assert.Equal(1, message.AggregateVersion);
        Assert.Equal("enterprise.collaboration.calendar-entry.created.v1", message.EventType);
        Assert.Equal("/enterprise/collaboration", message.Source);
        Assert.Equal($"calendar-entries/{created.Id}", message.Subject);
        Assert.Equal(correlationId, message.CorrelationId);
        Assert.NotEqual(Guid.Empty, message.EventId);
        Assert.Null(message.ProcessedAt);
        Assert.True(message.OccurredAt > DateTimeOffset.UtcNow.AddMinutes(-5));

        using var payload = JsonDocument.Parse(message.Payload);
        var root = payload.RootElement;
        Assert.Equal(ExpectedPayloadProperties.Order(), root.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(created.Id, root.GetProperty("EntryId").GetInt64());
        Assert.Equal("test-issuer", root.GetProperty("OwnerPrincipalIssuer").GetString());
        Assert.Equal("alice", root.GetProperty("OwnerPrincipalSubject").GetString());
        Assert.False(root.GetProperty("AllDay").GetBoolean());
        Assert.Equal(new DateTimeOffset(2026, 3, 10, 10, 0, 0, TimeSpan.Zero), root.GetProperty("StartAt").GetDateTimeOffset());
        Assert.Equal(new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero), root.GetProperty("EndAt").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("StartDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("EndDate").ValueKind);
        Assert.Equal("crm", root.GetProperty("LinkBoundedContext").GetString());
        Assert.Equal("opportunity", root.GetProperty("LinkEntityType").GetString());
        Assert.Equal(42, root.GetProperty("LinkEntityId").GetInt64());
    }

    [Fact]
    public async Task An_all_day_create_carries_iso_dates_and_no_instants()
    {
        var tenant = TestData.NextTenant();

        await _harness.CreateAsync(
            Commands.AllDay(tenant, Harness.Alice, "k-ob-ad", new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 12)));

        await using var admin = fixture.CreateAdminContext();
        using var payload = JsonDocument.Parse((await admin.OutboxMessages.SingleAsync(m => m.TenantId == tenant)).Payload);
        var root = payload.RootElement;
        Assert.True(root.GetProperty("AllDay").GetBoolean());
        Assert.Equal("2026-03-10", root.GetProperty("StartDate").GetString());
        Assert.Equal("2026-03-12", root.GetProperty("EndDate").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("StartAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("EndAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("LinkEntityId").ValueKind);
    }

    [Fact]
    public async Task Neither_the_outbox_payload_nor_the_stored_response_contains_the_title_or_notes()
    {
        var tenant = TestData.NextTenant();

        await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-priv", title: SecretTitle, notes: SecretNotes));

        await using var admin = fixture.CreateAdminContext();
        var outbox = await admin.OutboxMessages.SingleAsync(m => m.TenantId == tenant);
        var idempotency = await admin.IdempotencyRecords.SingleAsync(r => r.TenantId == tenant);
        foreach (var raw in new[] { outbox.Payload, idempotency.ResponsePayload, outbox.Subject, outbox.EventType })
        {
            Assert.DoesNotContain(SecretTitle, raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(SecretNotes, raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("layoff", raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("severance", raw, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain("Title", outbox.Payload);
        Assert.DoesNotContain("Notes", outbox.Payload);
    }

    [Fact]
    public async Task The_idempotency_record_stores_the_id_and_version_with_a_bounded_retention()
    {
        var tenant = TestData.NextTenant();

        var created = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-idem"));

        await using var admin = fixture.CreateAdminContext();
        var record = await admin.IdempotencyRecords.SingleAsync(r => r.TenantId == tenant);
        Assert.Equal("test-issuer", record.PrincipalIssuer);
        Assert.Equal("alice", record.PrincipalSubject);
        Assert.Equal("CreateCalendarEntry", record.Operation);
        Assert.Equal("k-idem", record.IdempotencyKey);
        Assert.Equal(201, record.ResponseStatus);
        Assert.Equal(64, record.RequestHash.Length);
        using var response = JsonDocument.Parse(record.ResponsePayload);
        Assert.Equal(new[] { "EntryId", "RowVersion" }, response.RootElement.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(created.Id, response.RootElement.GetProperty("EntryId").GetInt64());
        Assert.Equal(created.RowVersion, response.RootElement.GetProperty("RowVersion").GetInt64());
        var retention = record.ExpiresAt - record.CreatedAt;
        Assert.InRange(retention, TimeSpan.FromDays(6.9), TimeSpan.FromDays(7.1));
    }

    [Fact]
    public async Task A_replay_does_not_write_another_outbox_row()
    {
        var tenant = TestData.NextTenant();
        var command = Commands.Timed(tenant, Harness.Alice, "k-noreplay-ob");

        await _harness.CreateAsync(command);
        await _harness.CreateAsync(command);
        await _harness.CreateAsync(command);

        Assert.Equal(1, await _harness.CountOutboxAsync(tenant));
    }
}
