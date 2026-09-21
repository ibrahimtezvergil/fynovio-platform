using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Host.Tests.Fixtures;
using Xunit;

namespace Host.Tests;

/// <summary>`docs/plans/collaboration-calendar/api-contract.md` through the real stack: real logins, the real PDP, RLS as the
/// runtime role. Every test uses its own calendar day (200 days apart) so list assertions never see another test's entries.</summary>
[Collection(HostIntegrationCollection.Name)]
public sealed class CalendarEndpointsTests(CalendarApiFixture api) : IClassFixture<CalendarApiFixture>
{
    private const string Route = "/calendar/entries";
    private const string NewKey = "<new key>";

    private static int _dayCounter;

    private HttpClient Client => api.Client;
    private string Admin => api.AdminTenantOne;
    private string Rep => api.RepTenantOne;
    private string OtherTenant => api.AdminTenantTwo;
    private string NoRole => api.NoRoleTenantOne;

    // ---- authentication ----------------------------------------------------------------------------------------------

    public static TheoryData<string, string> AllRoutes => new()
    {
        { "GET", $"{Route}?from=2030-01-01T00:00:00Z&to=2030-01-02T00:00:00Z" },
        { "GET", $"{Route}/1" },
        { "POST", Route },
        { "PUT", $"{Route}/1" },
        { "DELETE", $"{Route}/1?expectedVersion=1" },
    };

    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task Every_route_is_unauthorized_without_a_bearer_token(string method, string path)
    {
        var response = await SendAsync(new HttpMethod(method), path, token: null, body: BodyFor(method));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task Every_route_rejects_a_bearer_token_that_is_not_ours(string method, string path)
    {
        var response = await SendAsync(new HttpMethod(method), path, token: "not.a.token", body: BodyFor(method));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- POST ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_answers_201_with_a_location_and_the_entry_reads_back_in_the_contract_shape()
    {
        var day = NextDay();

        var response = await SendAsync(HttpMethod.Post, Route, Admin, Timed(day));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await Json(response);
        var id = created.GetProperty("id").GetInt64();
        Assert.Equal(1, created.GetProperty("rowVersion").GetInt64());
        Assert.False(created.GetProperty("replayed").GetBoolean());
        Assert.Equal(["id", "replayed", "rowVersion"], PropertyNames(created));
        Assert.Equal($"{Route}/{id}", response.Headers.Location!.OriginalString);

        var entry = await ReadEntryAsync(Admin, id);
        Assert.Equal(
            ["allDay", "color", "endAt", "endDate", "id", "link", "notes", "rowVersion", "startAt", "startDate", "title"],
            PropertyNames(entry));
        Assert.Equal(id, entry.GetProperty("id").GetInt64());
        Assert.Equal(1, entry.GetProperty("rowVersion").GetInt64());
        Assert.Equal("Call with vendor", entry.GetProperty("title").GetString());
        Assert.Equal("Bring the price list", entry.GetProperty("notes").GetString());
        Assert.Equal("#3b82f6", entry.GetProperty("color").GetString());
        Assert.False(entry.GetProperty("allDay").GetBoolean());
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("startDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("endDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("link").ValueKind);
    }

    [Fact]
    public async Task Timed_values_come_back_as_utc_with_a_zero_offset_whatever_offset_was_sent()
    {
        var day = NextDay();
        var (viaOffset, _) = await CreateAsync(Admin, With(Timed(day), ("startAt", $"{Iso(day)}T09:00:00+03:00"), ("endAt", $"{Iso(day)}T11:30:00+03:00")));
        var (viaZ, _) = await CreateAsync(Admin, With(Timed(day), ("startAt", $"{Iso(day)}T06:00:00Z"), ("endAt", null)));

        var offset = await ReadEntryAsync(Admin, viaOffset);
        var z = await ReadEntryAsync(Admin, viaZ);

        Assert.Equal($"{Iso(day)}T06:00:00+00:00", offset.GetProperty("startAt").GetString());
        Assert.Equal($"{Iso(day)}T08:30:00+00:00", offset.GetProperty("endAt").GetString());
        Assert.Equal($"{Iso(day)}T06:00:00+00:00", z.GetProperty("startAt").GetString());
        Assert.Equal(JsonValueKind.Null, z.GetProperty("endAt").ValueKind);
    }

    [Fact]
    public async Task An_all_day_entry_round_trips_its_exclusive_end_date_and_carries_no_instants()
    {
        var day = NextDay();
        var (id, _) = await CreateAsync(Admin, AllDay(day));

        var entry = await ReadEntryAsync(Admin, id);
        var listed = (await ListAsync(Admin, Window(day))).GetProperty("items").EnumerateArray().Single();

        Assert.True(entry.GetProperty("allDay").GetBoolean());
        Assert.Equal(Iso(day), entry.GetProperty("startDate").GetString());
        Assert.Equal(Iso(day.AddDays(1)), entry.GetProperty("endDate").GetString());
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("startAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("endAt").ValueKind);
        Assert.Equal(Iso(day.AddDays(1)), listed.GetProperty("endDate").GetString());
    }

    [Fact]
    public async Task The_owner_and_tenant_come_from_the_caller_and_never_from_the_body()
    {
        var day = NextDay();
        var body = With(Timed(day), ("tenantId", 2), ("ownerSubject", "someone-else"), ("owner", new { issuer = "x", subject = "y" }));

        var (id, _) = await CreateAsync(Admin, body);

        Assert.Single((await ListAsync(Admin, Window(day))).GetProperty("items").EnumerateArray());
        Assert.Empty((await ListAsync(OtherTenant, Window(day))).GetProperty("items").EnumerateArray());
        Assert.Empty((await ListAsync(Rep, Window(day))).GetProperty("items").EnumerateArray());
        await AssertProblemAsync(await GetAsync($"{Route}/{id}", OtherTenant), HttpStatusCode.NotFound, "not_found");
    }

    public static IEnumerable<object?[]> InvalidTimedFields =>
    [
        ["title", null],
        ["title", ""],
        ["title", "   "],
        ["title", " padded "],
        ["title", "two\nlines"],
        ["title", "two\rlines"],
        ["title", "nul\0char"],
        ["title", new string('x', 201)],
        ["title", 42],
        ["notes", new string('n', 4001)],
        ["notes", "nul\0char"],
        ["color", null],
        ["color", "red"],
        ["color", "#12345"],
        ["color", "#12345g"],
        ["color", "#aabbcc\n"],
        ["startAt", "2030-01-01T09:00:00Z\n"],
        ["allDay", null],
        ["allDay", "yes"],
        ["startAt", null],
        ["startAt", "2030-01-01T09:00:00"],
        ["startAt", "2030-01-01T09:00"],
        ["startAt", "2030-01-01"],
        ["startAt", "2030-01-01T09:00:00+0300"],
        ["startAt", "2030-01-01T09:00:00+03"],
        ["startAt", "2030-01-01 09:00:00Z"],
        ["startAt", "2030-13-01T09:00:00Z"],
        ["startAt", "garbage"],
        ["startAt", 1893488400],
        ["endAt", "2030-01-01T10:00:00"],
        ["endAt", "2030-01-01"],
        ["endAt", "2030-01-01T10:00:00+0300"],
        ["endAt", "2030-01-01T08:00:00+03:00"],
        ["endAt", "2030-01-01T09:00:00+03:00"],
        ["startDate", "2030-01-01"],
        ["endDate", "2030-01-02"],
    ];

    [Theory]
    [MemberData(nameof(InvalidTimedFields))]
    public async Task Creating_a_timed_entry_with_an_invalid_field_is_a_400_validation_error_and_writes_nothing(string field, object? value)
    {
        var day = NextDay();
        var body = With(Timed(day), (field, value));
        if (field == "endAt" && value is string end && end.StartsWith("2030-01-01T", StringComparison.Ordinal))
            body["startAt"] = "2030-01-01T09:00:00+03:00"; // the relative-order cases need a fixed start

        var response = await SendAsync(HttpMethod.Post, Route, Admin, body);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
        Assert.Empty((await ListAsync(Admin, Window(day))).GetProperty("items").EnumerateArray());
    }

    public static IEnumerable<object?[]> InvalidAllDayFields =>
    [
        ["startDate", null],
        ["endDate", null],
        ["startDate", "2030/01/01"],
        ["startDate", "2030-13-45"],
        ["startDate", "2030-01-01T00:00:00Z"],
        ["endDate", "2030-01-01"],
        ["endDate", "2029-12-31"],
        ["startAt", "2030-01-01T09:00:00+03:00"],
        ["endAt", "2030-01-02T09:00:00+03:00"],
    ];

    [Theory]
    [MemberData(nameof(InvalidAllDayFields))]
    public async Task Creating_an_all_day_entry_with_an_invalid_field_is_a_400_validation_error(string field, object? value)
    {
        var body = With(AllDay(new DateOnly(2030, 1, 1)), (field, value));

        var response = await SendAsync(HttpMethod.Post, Route, Admin, body);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
    }

    [Theory]
    [InlineData("this is not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"title\":")]
    [InlineData("\"a string\"")]
    public async Task A_body_that_is_not_a_calendar_entry_object_is_a_400_validation_error(string raw)
    {
        var response = await SendAsync(HttpMethod.Post, Route, Admin, raw);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
    }

    [Fact]
    public async Task A_validation_failure_never_echoes_the_offending_text_back()
    {
        var marker = $"ECHO-{Guid.NewGuid():N}";

        var wrongType = await SendAsync(HttpMethod.Post, Route, Admin, $$"""{"title":"{{marker}}","allDay":"{{marker}}"}""");
        var multiLine = await SendAsync(HttpMethod.Post, Route, Admin, With(Timed(NextDay()), ("title", $"{marker}\n{marker}")));

        Assert.DoesNotContain(marker, await wrongType.Content.ReadAsStringAsync());
        Assert.DoesNotContain(marker, await multiLine.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_without_an_idempotency_key_is_a_400_validation_error()
    {
        var response = await SendAsync(HttpMethod.Post, Route, Admin, Timed(NextDay()), key: null);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("129")]
    public async Task A_blank_or_oversized_idempotency_key_is_a_400_validation_error(string key)
    {
        var day = NextDay();
        key = key == "129" ? new string('k', 129) : key;

        var response = await SendAsync(HttpMethod.Post, Route, Admin, Timed(day), key: key);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
        Assert.Empty((await ListAsync(Admin, Window(day))).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task An_idempotency_key_of_exactly_128_characters_is_accepted()
    {
        var response = await SendAsync(HttpMethod.Post, Route, Admin, Timed(NextDay()), key: new string('k', 128));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Two_idempotency_key_headers_are_a_400_validation_error()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Route) { Content = JsonContent.Create(Timed(NextDay())) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Admin);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", ["first-key", "second-key"]);

        var response = await Client.SendAsync(request);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
    }

    [Fact]
    public async Task Replaying_a_create_with_the_same_key_and_body_returns_the_original_result_marked_replayed()
    {
        var day = NextDay();
        var key = Guid.NewGuid().ToString();
        var first = await Json(await SendAsync(HttpMethod.Post, Route, Admin, Timed(day), key: key));

        var replay = await SendAsync(HttpMethod.Post, Route, Admin, Timed(day), key: key);

        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        var replayed = await Json(replay);
        Assert.True(replayed.GetProperty("replayed").GetBoolean());
        Assert.Equal(first.GetProperty("id").GetInt64(), replayed.GetProperty("id").GetInt64());
        Assert.Equal(first.GetProperty("rowVersion").GetInt64(), replayed.GetProperty("rowVersion").GetInt64());
        Assert.Equal($"{Route}/{first.GetProperty("id").GetInt64()}", replay.Headers.Location!.OriginalString);
        Assert.Single((await ListAsync(Admin, Window(day))).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Reusing_a_key_with_a_different_body_is_a_409_idempotency_key_reused()
    {
        var day = NextDay();
        var key = Guid.NewGuid().ToString();
        await SendAsync(HttpMethod.Post, Route, Admin, Timed(day, "First"), key: key);

        var reused = await SendAsync(HttpMethod.Post, Route, Admin, Timed(day, "Second"), key: key);

        await AssertProblemAsync(reused, HttpStatusCode.Conflict, "idempotency_key_reused");
        Assert.Single((await ListAsync(Admin, Window(day))).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task An_idempotency_key_belongs_to_its_caller_so_another_user_reusing_it_creates_their_own_entry()
    {
        var day = NextDay();
        var key = Guid.NewGuid().ToString();
        var admin = await Json(await SendAsync(HttpMethod.Post, Route, Admin, Timed(day), key: key));

        var rep = await Json(await SendAsync(HttpMethod.Post, Route, Rep, Timed(day), key: key));

        Assert.False(rep.GetProperty("replayed").GetBoolean());
        Assert.NotEqual(admin.GetProperty("id").GetInt64(), rep.GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task Concurrent_creates_with_the_same_key_produce_exactly_one_entry()
    {
        var day = NextDay();
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => SendAsync(HttpMethod.Post, Route, Admin, Timed(day), key: key)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var bodies = await Task.WhenAll(responses.Select(Json));
        Assert.Single(bodies.Select(b => b.GetProperty("id").GetInt64()).Distinct());
        Assert.Equal(1, bodies.Count(b => !b.GetProperty("replayed").GetBoolean()));
        Assert.Single((await ListAsync(Admin, Window(day))).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Create_by_a_member_without_the_collaboration_role_is_403_forbidden()
    {
        var day = NextDay();

        var response = await SendAsync(HttpMethod.Post, Route, NoRole, Timed(day));

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "forbidden");
    }

    /// <summary>Well-formed references no caller can resolve here: nothing with these ids exists, and the type is unknown.</summary>
    public static IEnumerable<object?[]> UnresolvableLinks =>
    [
        [new { boundedContext = "crm", entityType = "opportunity", id = 987_654 }],
        [new { boundedContext = "masterdata", entityType = "party", id = 987_654 }],
        [new { boundedContext = "nope", entityType = "nothing", id = 1 }],
        [new { boundedContext = "CRM", entityType = "Opportunity", id = 1 }],
    ];

    public static IEnumerable<object?[]> MalformedLinks =>
    [
        [new { boundedContext = "crm", entityType = "opportunity", id = -5 }],
        [new { boundedContext = "crm", entityType = "opportunity", id = 0 }],
        [new { boundedContext = "crm", entityType = "opportunity" }],
        [new { boundedContext = "crm", id = 3 }],
        [new { entityType = "opportunity", id = 3 }],
        [new { }],
    ];

    [Theory]
    [MemberData(nameof(UnresolvableLinks))]
    public async Task An_unresolvable_link_is_a_422_link_target_unavailable_and_nothing_is_stored(object link)
    {
        var day = NextDay();

        var response = await SendAsync(HttpMethod.Post, Route, Admin, With(Timed(day), ("link", link)));

        var problem = await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "link_target_unavailable");
        Assert.Equal("The link target is unavailable.", problem.GetProperty("title").GetString());
        Assert.Empty((await ListAsync(Admin, Window(day))).GetProperty("items").EnumerateArray());
    }

    [Theory]
    [MemberData(nameof(MalformedLinks))]
    public async Task A_malformed_link_is_a_400_validation_error_and_nothing_is_stored(object link)
    {
        var day = NextDay();

        var response = await SendAsync(HttpMethod.Post, Route, Admin, With(Timed(day), ("link", link)));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
        Assert.Empty((await ListAsync(Admin, Window(day))).GetProperty("items").EnumerateArray());
    }

    // ---- GET by id ----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_unknown_entry_another_owners_entry_and_another_tenants_entry_are_the_same_404()
    {
        var (id, _) = await CreateAsync(Admin, Timed(NextDay()));

        var otherOwner = await AssertProblemAsync(await GetAsync($"{Route}/{id}", Rep), HttpStatusCode.NotFound, "not_found");
        var otherTenant = await AssertProblemAsync(await GetAsync($"{Route}/{id}", OtherTenant), HttpStatusCode.NotFound, "not_found");
        var unknown = await AssertProblemAsync(await GetAsync($"{Route}/{id + 1_000_000}", Admin), HttpStatusCode.NotFound, "not_found");

        Assert.Equal(otherOwner.ToString(), otherTenant.ToString());
        Assert.Equal(otherOwner.GetProperty("title").GetString()!.Replace(id.ToString(), "#"),
            unknown.GetProperty("title").GetString()!.Replace((id + 1_000_000).ToString(), "#"));
    }

    [Fact]
    public async Task A_member_without_the_collaboration_role_cannot_read_an_entry_and_is_told_it_is_not_found()
    {
        var (id, _) = await CreateAsync(Admin, Timed(NextDay()));

        var response = await GetAsync($"{Route}/{id}", NoRole);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "not_found");
    }

    // ---- GET list -----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task List_wraps_the_entries_in_an_items_object_and_an_empty_window_is_an_empty_array()
    {
        var day = NextDay();

        var response = await GetAsync($"{Route}{Query(Window(day))}", Admin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Json(response);
        Assert.Equal(["items"], PropertyNames(body));
        Assert.Equal(JsonValueKind.Array, body.GetProperty("items").ValueKind);
        Assert.Empty(body.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task List_returns_only_the_callers_own_entries_in_the_callers_tenant()
    {
        var day = NextDay();
        var adminTitle = $"ADMIN-{Guid.NewGuid():N}";
        var repTitle = $"REP-{Guid.NewGuid():N}";
        await CreateAsync(Admin, Timed(day, adminTitle));
        await CreateAsync(Rep, Timed(day, repTitle));

        var adminList = await (await GetAsync($"{Route}{Query(Window(day))}", Admin)).Content.ReadAsStringAsync();
        var repList = await (await GetAsync($"{Route}{Query(Window(day))}", Rep)).Content.ReadAsStringAsync();
        var otherTenantList = await (await GetAsync($"{Route}{Query(Window(day))}", OtherTenant)).Content.ReadAsStringAsync();

        Assert.Contains(adminTitle, adminList);
        Assert.DoesNotContain(repTitle, adminList);
        Assert.Contains(repTitle, repList);
        Assert.DoesNotContain(adminTitle, repList);
        Assert.DoesNotContain(adminTitle, otherTenantList);
        Assert.DoesNotContain(repTitle, otherTenantList);
    }

    [Fact]
    public async Task A_window_of_exactly_100_days_is_accepted_and_101_days_is_422_range_too_large()
    {
        var from = new DateTimeOffset(2029, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var hundred = await GetAsync($"{Route}{Query((from, from.AddDays(100)))}", Admin);
        var hundredAndOne = await GetAsync($"{Route}{Query((from, from.AddDays(101)))}", Admin);

        Assert.Equal(HttpStatusCode.OK, hundred.StatusCode);
        await AssertProblemAsync(hundredAndOne, HttpStatusCode.UnprocessableEntity, "range_too_large");
    }

    [Theory]
    [InlineData("from=2030-01-02T00:00:00Z&to=2030-01-01T00:00:00Z")] // to before from
    [InlineData("from=2030-01-01T00:00:00Z&to=2030-01-01T00:00:00Z")] // empty window
    [InlineData("to=2030-01-02T00:00:00Z")] // no from
    [InlineData("from=2030-01-01T00:00:00Z")] // no to
    [InlineData("")] // neither
    [InlineData("from=2030-01-01T00:00:00&to=2030-01-02T00:00:00Z")] // offset-less from
    [InlineData("from=2030-01-01T00:00:00Z&to=2030-01-02T00:00:00")] // offset-less to
    [InlineData("from=2030-01-01&to=2030-01-02")] // dates only
    [InlineData("from=garbage&to=2030-01-02T00:00:00Z")]
    [InlineData("from=2030-01-01T00:00:00+03:00&to=2030-01-02T00:00:00Z")] // a raw + is a space in a query string
    [InlineData("from=2030-01-01T00:00:00Z&from=2030-01-01T00:00:00Z&to=2030-01-02T00:00:00Z")] // repeated
    public async Task A_missing_malformed_offset_less_or_mis_ordered_range_is_a_400_validation_error(string query)
    {
        var response = await GetAsync($"{Route}?{query}", Admin);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
    }

    [Fact]
    public async Task A_url_encoded_offset_in_the_range_is_accepted_and_means_the_same_instant()
    {
        var day = NextDay();
        var (id, _) = await CreateAsync(Admin, Timed(day)); // 06:00Z

        var from = Uri.EscapeDataString($"{Iso(day)}T08:00:00+03:00"); // 05:00Z
        var to = Uri.EscapeDataString($"{Iso(day)}T10:00:00+03:00"); // 07:00Z
        var response = await GetAsync($"{Route}?from={from}&to={to}", Admin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(id, (await Json(response)).GetProperty("items").EnumerateArray().Single().GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task List_by_a_member_without_the_collaboration_role_is_403_forbidden()
    {
        var response = await GetAsync($"{Route}{Query(Window(NextDay()))}", NoRole);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "forbidden");
    }

    // ---- PUT ----------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Update_replaces_every_field_bumps_the_version_and_clears_omitted_optionals()
    {
        var day = NextDay();
        var (id, version) = await CreateAsync(Admin, Timed(day));

        var response = await SendAsync(HttpMethod.Put, $"{Route}/{id}", Admin, Versioned(AllDay(day.AddDays(3), "Now all day"), version));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await Json(response);
        Assert.Equal(["id", "replayed", "rowVersion"], PropertyNames(result));
        Assert.Equal(id, result.GetProperty("id").GetInt64());
        Assert.Equal(version + 1, result.GetProperty("rowVersion").GetInt64());
        Assert.False(result.GetProperty("replayed").GetBoolean());

        var entry = await ReadEntryAsync(Admin, id);
        Assert.Equal("Now all day", entry.GetProperty("title").GetString());
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("notes").ValueKind);
        Assert.Equal("#10b981", entry.GetProperty("color").GetString());
        Assert.True(entry.GetProperty("allDay").GetBoolean());
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("startAt").ValueKind);
        Assert.Equal(Iso(day.AddDays(3)), entry.GetProperty("startDate").GetString());
        Assert.Equal(Iso(day.AddDays(4)), entry.GetProperty("endDate").GetString());
        Assert.Equal(version + 1, entry.GetProperty("rowVersion").GetInt64());
        Assert.DoesNotContain(
            (await ListAsync(Admin, Window(day))).GetProperty("items").EnumerateArray(),
            e => e.GetProperty("id").GetInt64() == id && !e.GetProperty("allDay").GetBoolean());
    }

    public static IEnumerable<object?[]> BadExpectedVersions =>
    [
        [null],
        [-1],
        ["abc"],
        ["3"],
        [1.5],
        [true],
    ];

    [Theory]
    [MemberData(nameof(BadExpectedVersions))]
    public async Task Update_with_a_missing_or_malformed_expected_version_is_a_400_validation_error(object? expectedVersion)
    {
        var (id, _) = await CreateAsync(Admin, Timed(NextDay()));

        var response = await SendAsync(HttpMethod.Put, $"{Route}/{id}", Admin, With(Timed(NextDay(), "Never"), ("expectedVersion", expectedVersion)));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
        await AssertUnchangedAsync(Admin, id, "Call with vendor", 1);
    }

    [Fact]
    public async Task Update_with_a_stale_version_is_a_409_concurrency_conflict_and_changes_nothing()
    {
        var (id, version) = await CreateAsync(Admin, Timed(NextDay()));
        await SendAsync(HttpMethod.Put, $"{Route}/{id}", Admin, Versioned(Timed(NextDay(), "Winner"), version));

        var stale = await SendAsync(HttpMethod.Put, $"{Route}/{id}", Admin, Versioned(Timed(NextDay(), "Loser"), version));
        var ahead = await SendAsync(HttpMethod.Put, $"{Route}/{id}", Admin, Versioned(Timed(NextDay(), "Loser"), version + 5));

        await AssertProblemAsync(stale, HttpStatusCode.Conflict, "concurrency_conflict");
        await AssertProblemAsync(ahead, HttpStatusCode.Conflict, "concurrency_conflict");
        await AssertUnchangedAsync(Admin, id, "Winner", version + 1);
    }

    [Fact]
    public async Task Two_concurrent_updates_from_the_same_version_let_exactly_one_win()
    {
        for (var round = 0; round < 3; round++)
        {
            var (id, version) = await CreateAsync(Admin, Timed(NextDay()));

            var responses = await Task.WhenAll(
                SendAsync(HttpMethod.Put, $"{Route}/{id}", Admin, Versioned(Timed(NextDay(), "Left"), version)),
                SendAsync(HttpMethod.Put, $"{Route}/{id}", Admin, Versioned(Timed(NextDay(), "Right"), version)));

            var winner = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            var loser = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            await AssertProblemAsync(loser, HttpStatusCode.Conflict, "concurrency_conflict");
            Assert.Equal(version + 1, (await Json(winner)).GetProperty("rowVersion").GetInt64());
            Assert.Equal(version + 1, (await ReadEntryAsync(Admin, id)).GetProperty("rowVersion").GetInt64());
        }
    }

    [Fact]
    public async Task Replaying_an_update_returns_the_original_result_and_a_different_body_under_the_key_is_a_409()
    {
        var day = NextDay();
        var (id, version) = await CreateAsync(Admin, Timed(day));
        var key = Guid.NewGuid().ToString();
        await SendAsync(HttpMethod.Put, $"{Route}/{id}", Admin, Versioned(Timed(day, "Once"), version), key: key);

        var replay = await SendAsync(HttpMethod.Put, $"{Route}/{id}", Admin, Versioned(Timed(day, "Once"), version), key: key);
        var reused = await SendAsync(HttpMethod.Put, $"{Route}/{id}", Admin, Versioned(Timed(day, "Twice"), version), key: key);

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayed = await Json(replay);
        Assert.True(replayed.GetProperty("replayed").GetBoolean());
        Assert.Equal(version + 1, replayed.GetProperty("rowVersion").GetInt64());
        await AssertProblemAsync(reused, HttpStatusCode.Conflict, "idempotency_key_reused");
        await AssertUnchangedAsync(Admin, id, "Once", version + 1);
    }

    [Fact]
    public async Task Concurrent_updates_with_the_same_key_apply_exactly_once()
    {
        var day = NextDay();
        var (id, version) = await CreateAsync(Admin, Timed(day));
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => SendAsync(HttpMethod.Put, $"{Route}/{id}", Admin, Versioned(Timed(day, "Raced"), version), key: key)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var bodies = await Task.WhenAll(responses.Select(Json));
        Assert.Equal(1, bodies.Count(b => !b.GetProperty("replayed").GetBoolean()));
        await AssertUnchangedAsync(Admin, id, "Raced", version + 1);
    }

    [Fact]
    public async Task Updating_an_unknown_entry_another_owners_or_another_tenants_is_a_404_and_changes_nothing()
    {
        var (id, version) = await CreateAsync(Admin, Timed(NextDay()));
        var body = Versioned(Timed(NextDay(), "Hijack"), version);

        var otherOwner = await AssertProblemAsync(await SendAsync(HttpMethod.Put, $"{Route}/{id}", Rep, body), HttpStatusCode.NotFound, "not_found");
        var otherTenant = await AssertProblemAsync(await SendAsync(HttpMethod.Put, $"{Route}/{id}", OtherTenant, body), HttpStatusCode.NotFound, "not_found");
        await AssertProblemAsync(await SendAsync(HttpMethod.Put, $"{Route}/{id + 1_000_000}", Admin, body), HttpStatusCode.NotFound, "not_found");

        Assert.Equal(otherOwner.ToString(), otherTenant.ToString());
        await AssertUnchangedAsync(Admin, id, "Call with vendor", version);
    }

    [Fact]
    public async Task Updating_without_the_collaboration_role_is_403_even_for_an_entry_that_does_not_exist()
    {
        var (id, version) = await CreateAsync(Admin, Timed(NextDay()));
        var body = Versioned(Timed(NextDay(), "Nope"), version);

        await AssertProblemAsync(await SendAsync(HttpMethod.Put, $"{Route}/{id}", NoRole, body), HttpStatusCode.Forbidden, "forbidden");
        await AssertProblemAsync(await SendAsync(HttpMethod.Put, $"{Route}/{id + 1_000_000}", NoRole, body), HttpStatusCode.Forbidden, "forbidden");
        await AssertUnchangedAsync(Admin, id, "Call with vendor", version);
    }

    [Fact]
    public async Task Update_validates_timed_values_and_the_key_and_a_rejected_request_does_not_consume_its_key()
    {
        var day = NextDay();
        var (id, version) = await CreateAsync(Admin, Timed(day));
        var key = Guid.NewGuid().ToString();

        var offsetLessStart = await SendAsync(HttpMethod.Put, $"{Route}/{id}", Admin, Versioned(With(Timed(day, "Fixed"), ("startAt", $"{Iso(day)}T09:00:00")), version), key: key);
        var offsetLessEnd = await SendAsync(HttpMethod.Put, $"{Route}/{id}", Admin, Versioned(With(Timed(day, "Fixed"), ("endAt", $"{Iso(day)}T10:00:00")), version), key: key);
        var noKey = await SendAsync(HttpMethod.Put, $"{Route}/{id}", Admin, Versioned(Timed(day, "Fixed"), version), key: null);

        await AssertProblemAsync(offsetLessStart, HttpStatusCode.BadRequest, "validation_error");
        await AssertProblemAsync(offsetLessEnd, HttpStatusCode.BadRequest, "validation_error");
        await AssertProblemAsync(noKey, HttpStatusCode.BadRequest, "validation_error");
        await AssertUnchangedAsync(Admin, id, "Call with vendor", version);

        var corrected = await SendAsync(HttpMethod.Put, $"{Route}/{id}", Admin, Versioned(Timed(day, "Fixed"), version), key: key);
        Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
        Assert.False((await Json(corrected)).GetProperty("replayed").GetBoolean());
    }

    [Theory]
    [MemberData(nameof(UnresolvableLinks))]
    public async Task Updating_with_an_unresolvable_link_is_a_422_link_target_unavailable_and_changes_nothing(object link)
    {
        var day = NextDay();
        var (id, version) = await CreateAsync(Admin, Timed(day));

        var response = await SendAsync(HttpMethod.Put, $"{Route}/{id}", Admin, Versioned(With(Timed(day, "Linked"), ("link", link)), version));

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "link_target_unavailable");
        await AssertUnchangedAsync(Admin, id, "Call with vendor", version);
    }

    [Theory]
    [MemberData(nameof(MalformedLinks))]
    public async Task Updating_with_a_malformed_link_is_a_400_validation_error_and_changes_nothing(object link)
    {
        var day = NextDay();
        var (id, version) = await CreateAsync(Admin, Timed(day));

        var response = await SendAsync(HttpMethod.Put, $"{Route}/{id}", Admin, Versioned(With(Timed(day, "Linked"), ("link", link)), version));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
        await AssertUnchangedAsync(Admin, id, "Call with vendor", version);
    }

    [Fact]
    public async Task Updating_with_an_explicit_null_link_or_no_link_at_all_is_accepted()
    {
        var day = NextDay();
        var (id, version) = await CreateAsync(Admin, Timed(day));
        var withoutLink = Versioned(Timed(day, "No link key"), version);
        withoutLink.Remove("link");

        var response = await SendAsync(HttpMethod.Put, $"{Route}/{id}", Admin, withoutLink);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- DELETE -------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Delete_answers_204_with_no_body_and_the_entry_is_gone()
    {
        var day = NextDay();
        var (id, version) = await CreateAsync(Admin, Timed(day));

        var response = await SendAsync(HttpMethod.Delete, $"{Route}/{id}?expectedVersion={version}", Admin);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        await AssertProblemAsync(await GetAsync($"{Route}/{id}", Admin), HttpStatusCode.NotFound, "not_found");
        Assert.Empty((await ListAsync(Admin, Window(day))).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Replaying_a_delete_answers_204_again_but_deleting_it_again_under_a_new_key_is_404()
    {
        var (id, version) = await CreateAsync(Admin, Timed(NextDay()));
        var key = Guid.NewGuid().ToString();
        await SendAsync(HttpMethod.Delete, $"{Route}/{id}?expectedVersion={version}", Admin, key: key);

        var replay = await SendAsync(HttpMethod.Delete, $"{Route}/{id}?expectedVersion={version}", Admin, key: key);
        var again = await SendAsync(HttpMethod.Delete, $"{Route}/{id}?expectedVersion={version}", Admin);

        Assert.Equal(HttpStatusCode.NoContent, replay.StatusCode);
        await AssertProblemAsync(again, HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Reusing_a_delete_key_for_a_different_expected_version_is_a_409_idempotency_key_reused()
    {
        var (id, version) = await CreateAsync(Admin, Timed(NextDay()));
        var key = Guid.NewGuid().ToString();
        await SendAsync(HttpMethod.Delete, $"{Route}/{id}?expectedVersion={version}", Admin, key: key);

        var reused = await SendAsync(HttpMethod.Delete, $"{Route}/{id}?expectedVersion={version + 1}", Admin, key: key);

        await AssertProblemAsync(reused, HttpStatusCode.Conflict, "idempotency_key_reused");
    }

    [Fact]
    public async Task Concurrent_deletes_with_the_same_key_delete_exactly_once()
    {
        var (id, version) = await CreateAsync(Admin, Timed(NextDay()));
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => SendAsync(HttpMethod.Delete, $"{Route}/{id}?expectedVersion={version}", Admin, key: key)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));
        await AssertProblemAsync(await GetAsync($"{Route}/{id}", Admin), HttpStatusCode.NotFound, "not_found");
    }

    [Theory]
    [InlineData("")]
    [InlineData("?expectedVersion=")]
    [InlineData("?expectedVersion=abc")]
    [InlineData("?expectedVersion=-1")]
    [InlineData("?expectedVersion=+1")]
    [InlineData("?expectedVersion=1.5")]
    [InlineData("?expectedVersion=99999999999999999999")]
    public async Task Delete_with_a_missing_or_malformed_expected_version_is_a_400_validation_error(string query)
    {
        var (id, version) = await CreateAsync(Admin, Timed(NextDay()));

        var response = await SendAsync(HttpMethod.Delete, $"{Route}/{id}{query}", Admin);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
        await AssertUnchangedAsync(Admin, id, "Call with vendor", version);
    }

    [Fact]
    public async Task Delete_without_an_idempotency_key_is_a_400_validation_error_and_the_entry_survives()
    {
        var (id, version) = await CreateAsync(Admin, Timed(NextDay()));

        var response = await SendAsync(HttpMethod.Delete, $"{Route}/{id}?expectedVersion={version}", Admin, key: null);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
        await AssertUnchangedAsync(Admin, id, "Call with vendor", version);
    }

    [Fact]
    public async Task Delete_with_a_stale_version_is_a_409_concurrency_conflict_and_the_entry_survives()
    {
        var (id, version) = await CreateAsync(Admin, Timed(NextDay()));
        await SendAsync(HttpMethod.Put, $"{Route}/{id}", Admin, Versioned(Timed(NextDay(), "Moved on"), version));

        var response = await SendAsync(HttpMethod.Delete, $"{Route}/{id}?expectedVersion={version}", Admin);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "concurrency_conflict");
        await AssertUnchangedAsync(Admin, id, "Moved on", version + 1);
    }

    [Fact]
    public async Task Deleting_an_unknown_entry_another_owners_or_another_tenants_is_a_404_and_the_entry_survives()
    {
        var (id, version) = await CreateAsync(Admin, Timed(NextDay()));

        await AssertProblemAsync(await SendAsync(HttpMethod.Delete, $"{Route}/{id}?expectedVersion={version}", Rep), HttpStatusCode.NotFound, "not_found");
        await AssertProblemAsync(await SendAsync(HttpMethod.Delete, $"{Route}/{id}?expectedVersion={version}", OtherTenant), HttpStatusCode.NotFound, "not_found");
        await AssertProblemAsync(await SendAsync(HttpMethod.Delete, $"{Route}/{id + 1_000_000}?expectedVersion=1", Admin), HttpStatusCode.NotFound, "not_found");
        await AssertUnchangedAsync(Admin, id, "Call with vendor", version);
    }

    [Fact]
    public async Task Deleting_without_the_collaboration_role_is_403_and_the_entry_survives()
    {
        var (id, version) = await CreateAsync(Admin, Timed(NextDay()));

        await AssertProblemAsync(await SendAsync(HttpMethod.Delete, $"{Route}/{id}?expectedVersion={version}", NoRole), HttpStatusCode.Forbidden, "forbidden");
        await AssertUnchangedAsync(Admin, id, "Call with vendor", version);
    }

    // ---- privacy ------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Titles_and_notes_never_reach_the_logs_on_success_or_on_any_failure_path()
    {
        var marker = $"PRIVATE-{Guid.NewGuid():N}";
        var day = NextDay();
        var body = With(Timed(day, $"{marker}-title"), ("notes", $"{marker}-notes"));

        var (id, version) = await CreateAsync(Admin, body);
        await SendAsync(HttpMethod.Put, $"{Route}/{id}", Admin, Versioned(With(Timed(day, $"{marker}-updated"), ("notes", $"{marker}-notes")), version));
        await SendAsync(HttpMethod.Put, $"{Route}/{id}", Admin, Versioned(With(Timed(day, $"{marker}-stale"), ("notes", $"{marker}-notes")), version)); // 409
        await SendAsync(HttpMethod.Put, $"{Route}/{id}", Rep, Versioned(Timed(day, $"{marker}-hijack"), version)); // 404
        await SendAsync(HttpMethod.Post, Route, Admin, With(Timed(day, $"{marker}\n{marker}"), ("notes", $"{marker}-notes"))); // 400
        await SendAsync(HttpMethod.Post, Route, Admin, With(Timed(day, $"{marker}-linked"), ("link", new { boundedContext = "crm", entityType = "opportunity", id = 1 }))); // 422
        await SendAsync(HttpMethod.Post, Route, NoRole, Timed(day, $"{marker}-forbidden")); // 403
        await SendAsync(HttpMethod.Post, Route, Admin, $$"""{"title":"{{marker}}-broken","allDay":"{{marker}}"}"""); // malformed
        await GetAsync($"{Route}/{id}", Admin);
        await GetAsync($"{Route}{Query(Window(day))}", Admin);
        await SendAsync(HttpMethod.Delete, $"{Route}/{id}?expectedVersion={version + 1}", Admin);

        Assert.NotEmpty(api.Logs.Entries); // the capture works, so an empty match below means something
        Assert.DoesNotContain(api.Logs.Entries, entry => entry.Contains(marker, StringComparison.Ordinal));
    }

    // ---- helpers ------------------------------------------------------------------------------------------------------

    private static DateOnly NextDay() => new DateOnly(2030, 1, 1).AddDays(Interlocked.Increment(ref _dayCounter) * 200);

    private static string Iso(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A window a few days either side of the day, wide enough for the entries the tests place on it.</summary>
    private static (DateTimeOffset From, DateTimeOffset To) Window(DateOnly day) =>
        (new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(-2),
         new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(6));

    private static string Query((DateTimeOffset From, DateTimeOffset To) window) =>
        $"?from={Uri.EscapeDataString(Utc(window.From))}&to={Uri.EscapeDataString(Utc(window.To))}";

    private static string Utc(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static Dictionary<string, object?> Timed(DateOnly day, string title = "Call with vendor") => new()
    {
        ["title"] = title,
        ["notes"] = "Bring the price list",
        ["color"] = "#3B82F6",
        ["allDay"] = false,
        ["startAt"] = $"{Iso(day)}T09:00:00+03:00",
        ["endAt"] = $"{Iso(day)}T10:00:00+03:00",
        ["startDate"] = null,
        ["endDate"] = null,
        ["link"] = null,
    };

    private static Dictionary<string, object?> AllDay(DateOnly day, string title = "Offsite") => new()
    {
        ["title"] = title,
        ["notes"] = null,
        ["color"] = "#10B981",
        ["allDay"] = true,
        ["startAt"] = null,
        ["endAt"] = null,
        ["startDate"] = Iso(day),
        ["endDate"] = Iso(day.AddDays(1)),
        ["link"] = null,
    };

    private static Dictionary<string, object?>? BodyFor(string method) =>
        method is "POST" or "PUT" ? Versioned(Timed(NextDay()), 1) : null;

    private static Dictionary<string, object?> With(Dictionary<string, object?> body, params (string Field, object? Value)[] changes)
    {
        foreach (var (field, value) in changes)
            body[field] = value;
        return body;
    }

    private static Dictionary<string, object?> Versioned(Dictionary<string, object?> body, long expectedVersion) =>
        With(body, ("expectedVersion", expectedVersion));

    /// <summary>Sends a request; `key` defaults to a fresh idempotency key, null sends none. A string body is sent verbatim.</summary>
    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token, object? body = null, string? key = NewKey)
    {
        var request = new HttpRequestMessage(method, path);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (key is not null)
            request.Headers.TryAddWithoutValidation("Idempotency-Key", key == NewKey ? Guid.NewGuid().ToString() : key);
        if (body is string raw)
            request.Content = new StringContent(raw, Encoding.UTF8, "application/json");
        else if (body is not null)
            request.Content = JsonContent.Create(body);
        return Client.SendAsync(request);
    }

    private Task<HttpResponseMessage> GetAsync(string path, string token) => SendAsync(HttpMethod.Get, path, token, key: null);

    private async Task<(long Id, long Version)> CreateAsync(string token, object body)
    {
        var response = await SendAsync(HttpMethod.Post, Route, token, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await Json(response);
        return (created.GetProperty("id").GetInt64(), created.GetProperty("rowVersion").GetInt64());
    }

    private async Task<JsonElement> ReadEntryAsync(string token, long id)
    {
        var response = await GetAsync($"{Route}/{id}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await Json(response);
    }

    private async Task<JsonElement> ListAsync(string token, (DateTimeOffset From, DateTimeOffset To) window)
    {
        var response = await GetAsync($"{Route}{Query(window)}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await Json(response);
    }

    private async Task AssertUnchangedAsync(string token, long id, string title, long version)
    {
        var entry = await ReadEntryAsync(token, id);
        Assert.Equal(title, entry.GetProperty("title").GetString());
        Assert.Equal(version, entry.GetProperty("rowVersion").GetInt64());
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static string[] PropertyNames(JsonElement element) => element.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Every error is the Host's `{ status, type, title }` problem and nothing else.</summary>
    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string type)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await Json(response);
        Assert.Equal(["status", "title", "type"], PropertyNames(body));
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        Assert.Equal(type, body.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("title").GetString()));
        return body;
    }
}
