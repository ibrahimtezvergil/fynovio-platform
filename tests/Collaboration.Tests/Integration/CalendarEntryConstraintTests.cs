using Contracts;
using Npgsql;
using NpgsqlTypes;
using Xunit;

namespace Collaboration.Tests.Integration;

/// <summary>Every single-row invariant of docs/schema/collaboration-schema.md must hold in the database itself,
/// not only in the aggregate (AGENTS.md). Rows are inserted with raw SQL through the admin role so the domain
/// cannot pre-empt the constraint; each rejection asserts the SQLSTATE and the constraint name.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class CalendarEntryConstraintTests(PostgresFixture fixture)
{
    private const string CheckViolation = "23514";
    private const string UniqueViolation = "23505";
    private const string StringTooLong = "22001";

    private static readonly DateTimeOffset Start = new(2026, 3, 10, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 3, 10);

    /// <summary>A valid timed row; each case mutates a copy.</summary>
    private sealed record Row(
        string Title = "Title", string? Notes = null, string Color = "#336699", bool AllDay = false,
        DateTimeOffset? StartAt = null, DateTimeOffset? EndAt = null, DateOnly? StartDate = null, DateOnly? EndDate = null,
        string? LinkContext = null, string? LinkType = null, long? LinkId = null)
    {
        public static Row ValidTimed => new() { StartAt = Start };
        public static Row ValidAllDay => new() { AllDay = true, StartDate = Day, EndDate = Day.AddDays(1) };
    }

    private static readonly Dictionary<string, (Row Row, string SqlState, string? Constraint)> Rejected = new()
    {
        // title
        ["title_empty"] = (Row.ValidTimed with { Title = "" }, CheckViolation, "ck_calendar_entries_title"),
        ["title_only_spaces"] = (Row.ValidTimed with { Title = "   " }, CheckViolation, "ck_calendar_entries_title"),
        ["title_leading_space"] = (Row.ValidTimed with { Title = " x" }, CheckViolation, "ck_calendar_entries_title"),
        ["title_trailing_space"] = (Row.ValidTimed with { Title = "x " }, CheckViolation, "ck_calendar_entries_title"),
        ["title_line_feed"] = (Row.ValidTimed with { Title = "a\nb" }, CheckViolation, "ck_calendar_entries_title"),
        ["title_carriage_return"] = (Row.ValidTimed with { Title = "a\rb" }, CheckViolation, "ck_calendar_entries_title"),
        // varchar(200) fires before the CHECK's upper bound can, so the column type is what enforces > 200
        ["title_201_chars"] = (Row.ValidTimed with { Title = new string('x', 201) }, StringTooLong, null),

        // notes
        ["notes_4001_chars"] = (Row.ValidTimed with { Notes = new string('n', 4001) }, CheckViolation, "ck_calendar_entries_notes"),

        // color
        ["color_uppercase"] = (Row.ValidTimed with { Color = "#AABBCC" }, CheckViolation, "ck_calendar_entries_color"),
        ["color_missing_hash"] = (Row.ValidTimed with { Color = "aabbccd" }, CheckViolation, "ck_calendar_entries_color"),
        ["color_short"] = (Row.ValidTimed with { Color = "#abc" }, CheckViolation, "ck_calendar_entries_color"),
        ["color_non_hex"] = (Row.ValidTimed with { Color = "#gggggg" }, CheckViolation, "ck_calendar_entries_color"),
        ["color_8_chars"] = (Row.ValidTimed with { Color = "#aabbccd" }, StringTooLong, null),

        // all-day / timed field combinations
        ["all_day_with_start_at"] = (Row.ValidAllDay with { StartAt = Start }, CheckViolation, "ck_calendar_entries_all_day_timing"),
        ["all_day_with_end_at"] = (Row.ValidAllDay with { EndAt = Start.AddHours(1) }, CheckViolation, "ck_calendar_entries_all_day_timing"),
        ["all_day_without_start_date"] = (Row.ValidAllDay with { StartDate = null }, CheckViolation, "ck_calendar_entries_all_day_timing"),
        ["all_day_without_end_date"] = (Row.ValidAllDay with { EndDate = null }, CheckViolation, "ck_calendar_entries_all_day_timing"),
        ["timed_without_start_at"] = (Row.ValidTimed with { StartAt = null }, CheckViolation, "ck_calendar_entries_all_day_timing"),
        ["timed_with_start_date"] = (Row.ValidTimed with { StartDate = Day }, CheckViolation, "ck_calendar_entries_all_day_timing"),
        ["timed_with_end_date"] = (Row.ValidTimed with { EndDate = Day.AddDays(1) }, CheckViolation, "ck_calendar_entries_all_day_timing"),

        // exclusive ends
        ["end_at_equal_to_start"] = (Row.ValidTimed with { EndAt = Start }, CheckViolation, "ck_calendar_entries_end_at"),
        ["end_at_before_start"] = (Row.ValidTimed with { EndAt = Start.AddMinutes(-1) }, CheckViolation, "ck_calendar_entries_end_at"),
        ["end_date_equal_to_start"] = (Row.ValidAllDay with { EndDate = Day }, CheckViolation, "ck_calendar_entries_end_date"),
        ["end_date_before_start"] = (Row.ValidAllDay with { EndDate = Day.AddDays(-1) }, CheckViolation, "ck_calendar_entries_end_date"),

        // link: all or none, positive id
        ["link_only_context"] = (Row.ValidTimed with { LinkContext = "crm" }, CheckViolation, "ck_calendar_entries_link"),
        ["link_only_type"] = (Row.ValidTimed with { LinkType = "opportunity" }, CheckViolation, "ck_calendar_entries_link"),
        ["link_only_id"] = (Row.ValidTimed with { LinkId = 5 }, CheckViolation, "ck_calendar_entries_link"),
        ["link_context_and_type_without_id"] = (Row.ValidTimed with { LinkContext = "crm", LinkType = "opportunity" }, CheckViolation, "ck_calendar_entries_link"),
        ["link_context_and_id_without_type"] = (Row.ValidTimed with { LinkContext = "crm", LinkId = 5 }, CheckViolation, "ck_calendar_entries_link"),
        ["link_type_and_id_without_context"] = (Row.ValidTimed with { LinkType = "opportunity", LinkId = 5 }, CheckViolation, "ck_calendar_entries_link"),
        ["link_id_zero"] = (Row.ValidTimed with { LinkContext = "crm", LinkType = "opportunity", LinkId = 0 }, CheckViolation, "ck_calendar_entries_link"),
        ["link_id_negative"] = (Row.ValidTimed with { LinkContext = "crm", LinkType = "opportunity", LinkId = -1 }, CheckViolation, "ck_calendar_entries_link"),

        // link identifier grammar ^[a-z][a-z0-9_]*$
        ["link_context_uppercase"] = (Row.ValidTimed with { LinkContext = "CRM", LinkType = "opportunity", LinkId = 5 }, CheckViolation, "ck_calendar_entries_link_identifiers"),
        ["link_context_leading_digit"] = (Row.ValidTimed with { LinkContext = "1crm", LinkType = "opportunity", LinkId = 5 }, CheckViolation, "ck_calendar_entries_link_identifiers"),
        ["link_context_empty"] = (Row.ValidTimed with { LinkContext = "", LinkType = "opportunity", LinkId = 5 }, CheckViolation, "ck_calendar_entries_link_identifiers"),
        ["link_type_uppercase"] = (Row.ValidTimed with { LinkContext = "crm", LinkType = "Opportunity", LinkId = 5 }, CheckViolation, "ck_calendar_entries_link_identifiers"),
        ["link_type_hyphen"] = (Row.ValidTimed with { LinkContext = "crm", LinkType = "sales-order", LinkId = 5 }, CheckViolation, "ck_calendar_entries_link_identifiers"),
        ["link_type_with_space"] = (Row.ValidTimed with { LinkContext = "crm", LinkType = "sales order", LinkId = 5 }, CheckViolation, "ck_calendar_entries_link_identifiers"),
    };

    private static readonly Dictionary<string, Row> Accepted = new()
    {
        ["timed_point_in_time"] = Row.ValidTimed,
        ["timed_with_end"] = Row.ValidTimed with { EndAt = Start.AddMinutes(1) },
        ["all_day_single_day"] = Row.ValidAllDay,
        ["all_day_multi_day"] = Row.ValidAllDay with { EndDate = Day.AddDays(30) },
        ["title_exactly_200_chars"] = Row.ValidTimed with { Title = new string('x', 200) },
        ["title_single_char"] = Row.ValidTimed with { Title = "x" },
        ["title_with_inner_spaces"] = Row.ValidTimed with { Title = "a b  c" },
        ["notes_exactly_4000_chars"] = Row.ValidTimed with { Notes = new string('n', 4000) },
        ["notes_multiline"] = Row.ValidTimed with { Notes = "line1\nline2\r\nline3" },
        ["color_lowercase_hex"] = Row.ValidTimed with { Color = "#0a9fF0".ToLowerInvariant() },
        ["link_complete"] = Row.ValidTimed with { LinkContext = "crm", LinkType = "opportunity", LinkId = 5 },
        ["link_identifier_with_digits_and_underscore"] = Row.ValidTimed with { LinkContext = "master_data2", LinkType = "party_v1", LinkId = 1 },
    };

    public static TheoryData<string> RejectedCases => new(Rejected.Keys);
    public static TheoryData<string> AcceptedCases => new(Accepted.Keys);

    [Theory]
    [MemberData(nameof(RejectedCases))]
    public async Task An_invalid_row_is_rejected_by_the_database(string caseName)
    {
        var (row, sqlState, constraint) = Rejected[caseName];

        var ex = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(row));

        Assert.Equal(sqlState, ex.SqlState);
        Assert.Equal(constraint, ex.ConstraintName);
    }

    [Theory]
    [MemberData(nameof(AcceptedCases))]
    public async Task A_valid_row_is_accepted_so_the_rejections_are_about_the_constraint(string caseName) =>
        await InsertAsync(Accepted[caseName]);

    [Fact]
    public async Task The_tenant_and_id_alternate_key_exists_and_ids_cannot_repeat()
    {
        await using var connection = await OpenAdminAsync();
        await using var alternateKey = new NpgsqlCommand("""
            SELECT count(*) FROM pg_constraint
            WHERE conrelid = 'collaboration.calendar_entries'::regclass AND contype = 'u' AND conname = 'ak_calendar_entries_tenant_id_id'
            """, connection);
        Assert.Equal(1L, await alternateKey.ExecuteScalarAsync());

        var tenant = TestData.NextTenant();
        await using var first = InsertCommand(connection, Row.ValidTimed, tenant);
        await first.ExecuteNonQueryAsync();
        await using var idOfFirst = new NpgsqlCommand(
            "SELECT id FROM collaboration.calendar_entries WHERE tenant_id = @t", connection);
        idOfFirst.Parameters.AddWithValue("t", tenant.Value);
        var id = (long)(await idOfFirst.ExecuteScalarAsync())!;

        await using var duplicate = InsertCommand(connection, Row.ValidTimed, tenant, id);
        var ex = await Assert.ThrowsAsync<PostgresException>(() => duplicate.ExecuteNonQueryAsync());
        Assert.Equal(UniqueViolation, ex.SqlState);
    }

    [Fact]
    public async Task The_idempotency_primary_key_rejects_a_duplicate_but_allows_a_different_key_operation_or_principal()
    {
        var tenant = TestData.NextTenant();
        await using var connection = await OpenAdminAsync();
        await InsertIdempotencyAsync(connection, tenant, "issuer", "subject", "Op", "key");

        var duplicate = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertIdempotencyAsync(connection, tenant, "issuer", "subject", "Op", "key"));
        Assert.Equal(UniqueViolation, duplicate.SqlState);
        Assert.Equal("pk_idempotency_records", duplicate.ConstraintName);

        await InsertIdempotencyAsync(connection, tenant, "issuer", "subject", "Op", "other-key");
        await InsertIdempotencyAsync(connection, tenant, "issuer", "subject", "Op2", "key");
        await InsertIdempotencyAsync(connection, tenant, "issuer", "other-subject", "Op", "key");
        await InsertIdempotencyAsync(connection, TestData.NextTenant(), "issuer", "subject", "Op", "key");
    }

    [Fact]
    public async Task The_outbox_event_id_is_unique()
    {
        var tenant = TestData.NextTenant();
        var eventId = Guid.NewGuid();
        await using var connection = await OpenAdminAsync();
        await InsertOutboxAsync(connection, tenant, eventId);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => InsertOutboxAsync(connection, tenant, eventId));

        Assert.Equal(UniqueViolation, ex.SqlState);
        Assert.Equal("ix_outbox_messages_event_id", ex.ConstraintName);
    }

    private async Task InsertAsync(Row row)
    {
        await using var connection = await OpenAdminAsync();
        await using var command = InsertCommand(connection, row, TestData.NextTenant());
        await command.ExecuteNonQueryAsync();
    }

    private async Task<NpgsqlConnection> OpenAdminAsync()
    {
        var connection = new NpgsqlConnection(fixture.AdminConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static NpgsqlCommand InsertCommand(NpgsqlConnection connection, Row row, TenantId tenant, long? id = null)
    {
        var command = new NpgsqlCommand($"""
            INSERT INTO collaboration.calendar_entries
                ({(id is null ? "" : "id, ")}tenant_id, owner_principal_issuer, owner_principal_subject, title, notes, color, all_day,
                 start_at, end_at, start_date, end_date, link_bounded_context, link_entity_type, link_entity_id,
                 row_version, created_at, updated_at)
            VALUES ({(id is null ? "" : "@id, ")}@tenant, 'issuer', 'subject', @title, @notes, @color, @all_day,
                 @start_at, @end_at, @start_date, @end_date, @link_context, @link_type, @link_id,
                 1, now(), now())
            """, connection);
        if (id is not null) command.Parameters.AddWithValue("id", id.Value);
        command.Parameters.AddWithValue("tenant", tenant.Value);
        command.Parameters.AddWithValue("title", row.Title);
        Add(command, "notes", NpgsqlDbType.Text, row.Notes);
        command.Parameters.AddWithValue("color", row.Color);
        command.Parameters.AddWithValue("all_day", row.AllDay);
        Add(command, "start_at", NpgsqlDbType.TimestampTz, row.StartAt?.UtcDateTime);
        Add(command, "end_at", NpgsqlDbType.TimestampTz, row.EndAt?.UtcDateTime);
        Add(command, "start_date", NpgsqlDbType.Date, row.StartDate);
        Add(command, "end_date", NpgsqlDbType.Date, row.EndDate);
        Add(command, "link_context", NpgsqlDbType.Text, row.LinkContext);
        Add(command, "link_type", NpgsqlDbType.Text, row.LinkType);
        Add(command, "link_id", NpgsqlDbType.Bigint, row.LinkId);
        return command;
    }

    private static void Add(NpgsqlCommand command, string name, NpgsqlDbType type, object? value) =>
        command.Parameters.Add(new NpgsqlParameter(name, type) { Value = value ?? DBNull.Value });

    private static async Task InsertIdempotencyAsync(
        NpgsqlConnection connection, TenantId tenant, string issuer, string subject, string operation, string key)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO collaboration.idempotency_records
                (tenant_id, principal_issuer, principal_subject, operation, idempotency_key, request_hash, response_status, response_payload, created_at, expires_at)
            VALUES (@t, @i, @s, @o, @k, 'hash', 201, '{}'::jsonb, now(), now() + interval '1 day')
            """, connection);
        command.Parameters.AddWithValue("t", tenant.Value);
        command.Parameters.AddWithValue("i", issuer);
        command.Parameters.AddWithValue("s", subject);
        command.Parameters.AddWithValue("o", operation);
        command.Parameters.AddWithValue("k", key);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertOutboxAsync(NpgsqlConnection connection, TenantId tenant, Guid eventId)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO collaboration.outbox_messages
                (tenant_id, aggregate_type, aggregate_id, aggregate_version, event_id, event_type, source, subject, correlation_id, payload, occurred_at)
            VALUES (@t, 'CalendarEntry', 1, 1, @e, 'type', '/source', 'subject/1', gen_random_uuid(), '{}'::jsonb, now())
            """, connection);
        command.Parameters.AddWithValue("t", tenant.Value);
        command.Parameters.AddWithValue("e", eventId);
        await command.ExecuteNonQueryAsync();
    }
}
