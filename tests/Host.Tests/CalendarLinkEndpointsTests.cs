using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Collaboration.Application;
using CRM.Application;
using Host.Tests.Fixtures;
using Xunit;

namespace Host.Tests;

/// <summary>D6 through the real stack — real logins, the real PDP, the real CRM resolvers behind the link directory, RLS as
/// the runtime role: a link is validated when written (every failure is the same 422, byte for byte) and hydrated for the
/// reader's own authorization when read.</summary>
[Collection(HostIntegrationCollection.Name)]
public sealed class CalendarLinkEndpointsTests(CalendarApiFixture api) : IClassFixture<CalendarApiFixture>
{
    private const string Route = "/calendar/entries";

    private static int _dayCounter;

    private HttpClient Client => api.Client;
    private string Admin => api.AdminTenantOne;
    private string Rep => api.RepTenantOne;
    private string OtherTenant => api.AdminTenantTwo;

    private static readonly string[] CalendarActions =
    [
        CollaborationActionKeys.CalendarEntryCreate, CollaborationActionKeys.CalendarEntryRead, CollaborationActionKeys.CalendarEntryList,
        CollaborationActionKeys.CalendarEntryUpdate, CollaborationActionKeys.CalendarEntryDelete,
    ];

    // ---- opportunities: write path ------------------------------------------------------------------------------------

    [Fact]
    public async Task Every_reason_a_link_cannot_be_resolved_is_the_same_422_byte_for_byte()
    {
        var opportunityId = await CreateOpportunityAsync(Admin, "Acme");
        var foreignOpportunityId = await CreateOpportunityAsync(OtherTenant, "Umbrella");
        var calendarOnly = await api.SeedMemberAsync(CalendarActions);

        var reasons = new Dictionary<string, (string Token, object Link)>
        {
            ["denied (a real opportunity, no CRM grant)"] = (calendarOnly.Token, Opportunity(opportunityId)),
            ["missing"] = (Admin, Opportunity(987_654)),
            ["another tenant's"] = (Admin, Opportunity(foreignOpportunityId)),
            ["unknown type"] = (Admin, new { boundedContext = "nope", entityType = "nothing", id = opportunityId }),
            ["known context, unknown type"] = (Admin, new { boundedContext = "crm", entityType = "invoice", id = opportunityId }),
        };

        var answers = new Dictionary<string, (HttpStatusCode Status, string Body)>();
        foreach (var (reason, (token, link)) in reasons)
        {
            var response = await SendAsync(HttpMethod.Post, Route, token, Timed(NextDay(), link));
            answers[reason] = (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        Assert.All(answers, answer => Assert.Equal(HttpStatusCode.UnprocessableEntity, answer.Value.Status));
        var (_, reference) = answers.First().Value;
        Assert.Contains("link_target_unavailable", reference);
        Assert.All(answers, answer => Assert.True(reference == answer.Value.Body, $"'{answer.Key}' answered differently: {answer.Value.Body}"));
    }

    [Fact]
    public async Task The_same_422_answers_an_update_and_changes_nothing()
    {
        var day = NextDay();
        var calendarOnly = await api.SeedMemberAsync(CalendarActions);
        var opportunityId = await CreateOpportunityAsync(Admin, "Acme");
        var (id, version) = await CreateAsync(calendarOnly.Token, Timed(day, null));

        var response = await SendAsync(HttpMethod.Put, $"{Route}/{id}", calendarOnly.Token, Versioned(Timed(day, Opportunity(opportunityId), "Linked"), version));

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "link_target_unavailable");
        var entry = await ReadAsync(calendarOnly.Token, id);
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("link").ValueKind);
        Assert.Equal(version, entry.GetProperty("rowVersion").GetInt64());
    }

    [Fact]
    public async Task Members_of_the_same_tenant_with_the_CRM_read_grant_can_link_each_others_opportunities()
    {
        var adminsOpportunity = await CreateOpportunityAsync(Admin, "Acme");
        var repsOpportunity = await CreateOpportunityAsync(Rep, "Globex");

        var repLinksAdmins = await SendAsync(HttpMethod.Post, Route, Rep, Timed(NextDay(), Opportunity(adminsOpportunity)));
        var adminLinksReps = await SendAsync(HttpMethod.Post, Route, Admin, Timed(NextDay(), Opportunity(repsOpportunity)));

        Assert.Equal(HttpStatusCode.Created, repLinksAdmins.StatusCode);
        Assert.Equal(HttpStatusCode.Created, adminLinksReps.StatusCode);
    }

    // ---- opportunities: read path -------------------------------------------------------------------------------------

    [Fact]
    public async Task An_accessible_opportunity_link_reads_back_with_its_label_for_the_owner_only()
    {
        var opportunityId = await CreateOpportunityAsync(Admin, "Acme");
        var day = NextDay();
        var (id, _) = await CreateAsync(Admin, Timed(day, Opportunity(opportunityId)));

        var entry = await ReadAsync(Admin, id);
        var listed = (await ListAsync(Admin, day)).GetProperty("items").EnumerateArray().Single();

        foreach (var view in new[] { entry, listed })
        {
            var link = view.GetProperty("link");
            Assert.Equal("accessible", link.GetProperty("state").GetString());
            Assert.Equal(["crm", "opportunity", opportunityId], RefOf(link));
            Assert.Equal($"#{opportunityId} · Acme Corporation", link.GetProperty("label").GetString());
        }

        // Another member of the tenant cannot even see that the entry exists, let alone its link.
        await AssertProblemAsync(await GetAsync($"{Route}/{id}", Rep), HttpStatusCode.NotFound, "not_found");
        Assert.Empty((await ListAsync(Rep, day)).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Losing_the_CRM_grant_turns_the_link_unavailable_without_label_and_the_entry_stays_editable()
    {
        var opportunityId = await CreateOpportunityAsync(Admin, "Acme");
        var other = await CreateOpportunityAsync(Admin, "Globex");
        var member = await api.SeedMemberAsync([.. CalendarActions, CrmActionKeys.OpportunityRead, CrmActionKeys.OpportunityList]);
        var day = NextDay();
        var key = Guid.NewGuid().ToString();
        var body = Timed(day, Opportunity(opportunityId));

        var created = await SendAsync(HttpMethod.Post, Route, member.Token, body, key);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdBody = await Json(created);
        var id = createdBody.GetProperty("id").GetInt64();

        // The member holds only the read grant, not the party grant, so the label must not carry the party's name.
        var before = (await ReadAsync(member.Token, id)).GetProperty("link");
        Assert.Equal("accessible", before.GetProperty("state").GetString());
        Assert.Equal($"#{opportunityId}", before.GetProperty("label").GetString());

        await api.RevokeAsync(member, CrmActionKeys.OpportunityRead);

        // Read: the same entry, the same reference, no label and no subtitle key at all (single and list).
        foreach (var view in new[] { await ReadAsync(member.Token, id), (await ListAsync(member.Token, day)).GetProperty("items").EnumerateArray().Single() })
        {
            var link = view.GetProperty("link");
            Assert.Equal("unavailable", link.GetProperty("state").GetString());
            Assert.Equal(["crm", "opportunity", opportunityId], RefOf(link));
            Assert.Equal(["ref", "state"], PropertyNames(link));
        }

        // A retry of the create replays even though the target no longer resolves.
        var replay = await SendAsync(HttpMethod.Post, Route, member.Token, body, key);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.True((await Json(replay)).GetProperty("replayed").GetBoolean());

        // Editing the entry keeps the link (unchanged links are not re-validated); pointing it elsewhere is validated (and denied).
        var version = (await ReadAsync(member.Token, id)).GetProperty("rowVersion").GetInt64();
        var renamed = await SendAsync(HttpMethod.Put, $"{Route}/{id}", member.Token, Versioned(Timed(day, Opportunity(opportunityId), "Renamed"), version));
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var redirected = await SendAsync(HttpMethod.Put, $"{Route}/{id}", member.Token, Versioned(Timed(day, Opportunity(other), "Elsewhere"), version + 1));
        await AssertProblemAsync(redirected, HttpStatusCode.UnprocessableEntity, "link_target_unavailable");

        var current = await ReadAsync(member.Token, id);
        Assert.Equal("Renamed", current.GetProperty("title").GetString());
        Assert.Equal(["crm", "opportunity", opportunityId], RefOf(current.GetProperty("link")));

        // Clearing the link is always allowed.
        var cleared = await SendAsync(HttpMethod.Put, $"{Route}/{id}", member.Token, Versioned(Timed(day, null, "Cleared"), version + 1));
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await ReadAsync(member.Token, id)).GetProperty("link").ValueKind);
    }

    [Fact]
    public async Task An_update_can_change_the_link_to_another_accessible_opportunity()
    {
        var first = await CreateOpportunityAsync(Admin, "Acme");
        var second = await CreateOpportunityAsync(Admin, "Globex");
        var day = NextDay();
        var (id, version) = await CreateAsync(Admin, Timed(day, Opportunity(first)));

        var response = await SendAsync(HttpMethod.Put, $"{Route}/{id}", Admin, Versioned(Timed(day, Opportunity(second)), version));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["crm", "opportunity", second], RefOf((await ReadAsync(Admin, id)).GetProperty("link")));
    }

    [Fact]
    public async Task A_label_never_reaches_the_logs()
    {
        var opportunityId = await CreateOpportunityAsync(Admin, "Acme");
        var day = NextDay();
        var (id, _) = await CreateAsync(Admin, Timed(day, Opportunity(opportunityId)));
        await ReadAsync(Admin, id);
        await ListAsync(Admin, day);

        Assert.NotEmpty(api.Logs.Entries);
        Assert.DoesNotContain(api.Logs.Entries, entry => entry.Contains($"#{opportunityId} · Acme", StringComparison.Ordinal));
    }

    // ---- parties (masterdata/party, answered by CRM under L-3) --------------------------------------------------------

    [Fact]
    public async Task A_party_link_is_accessible_with_the_display_name_and_no_subtitle_for_a_caller_who_may_read_parties()
    {
        var partyId = await PartyIdAsync(Admin, "Ada");
        var (id, _) = await CreateAsync(Admin, Timed(NextDay(), Party(partyId)));

        var link = (await ReadAsync(Admin, id)).GetProperty("link");

        Assert.Equal("accessible", link.GetProperty("state").GetString());
        Assert.Equal(["masterdata", "party", partyId], RefOf(link));
        Assert.Equal("Ada Lovelace", link.GetProperty("label").GetString());
        Assert.Equal(["label", "ref", "state"], PropertyNames(link));
    }

    [Fact]
    public async Task A_party_link_is_refused_for_a_caller_without_party_access_a_missing_party_and_another_tenants_party()
    {
        var partyId = await PartyIdAsync(Admin, "Ada");
        var foreignPartyId = await PartyIdAsync(OtherTenant, "Umbrella");
        var calendarOnly = await api.SeedMemberAsync(CalendarActions);

        var refusals = new[]
        {
            await SendAsync(HttpMethod.Post, Route, calendarOnly.Token, Timed(NextDay(), Party(partyId))),
            await SendAsync(HttpMethod.Post, Route, Admin, Timed(NextDay(), Party(987_654))),
            await SendAsync(HttpMethod.Post, Route, Admin, Timed(NextDay(), Party(foreignPartyId))),
        };

        var bodies = new List<string>();
        foreach (var refusal in refusals)
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, refusal.StatusCode);
            bodies.Add(await refusal.Content.ReadAsStringAsync());
        }
        Assert.Single(bodies.Distinct());
        Assert.Contains("link_target_unavailable", bodies[0]);
    }

    // ---- helpers ------------------------------------------------------------------------------------------------------

    private static int NextDayNumber() => Interlocked.Increment(ref _dayCounter);

    /// <summary>Its own calendar day per call (a different range from the other calendar test class), so list assertions never
    /// see another test's entries.</summary>
    private static DateOnly NextDay() => new DateOnly(2031, 1, 1).AddDays(NextDayNumber() * 200);

    private static string Iso(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Utc(DateTimeOffset instant) => instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static object Opportunity(long id) => new { boundedContext = "crm", entityType = "opportunity", id };

    private static object Party(long id) => new { boundedContext = "masterdata", entityType = "party", id };

    private static Dictionary<string, object?> Timed(DateOnly day, object? link, string title = "Call with vendor") => new()
    {
        ["title"] = title,
        ["notes"] = null,
        ["color"] = "#3b82f6",
        ["allDay"] = false,
        ["startAt"] = $"{Iso(day)}T09:00:00+03:00",
        ["endAt"] = $"{Iso(day)}T10:00:00+03:00",
        ["startDate"] = null,
        ["endDate"] = null,
        ["link"] = link,
    };

    private static Dictionary<string, object?> Versioned(Dictionary<string, object?> body, long expectedVersion)
    {
        body["expectedVersion"] = expectedVersion;
        return body;
    }

    private static object?[] RefOf(JsonElement link)
    {
        var reference = link.GetProperty("ref");
        return [reference.GetProperty("boundedContext").GetString(), reference.GetProperty("entityType").GetString(), reference.GetProperty("id").GetInt64()];
    }

    private static string[] PropertyNames(JsonElement element) => element.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();

    private async Task<long> PartyIdAsync(string token, string search)
    {
        var response = await SendAsync(HttpMethod.Get, $"/crm/references/parties?search={Uri.EscapeDataString(search)}&take=1", token, key: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await Json(response)).EnumerateArray().First().GetProperty("id").GetInt64();
    }

    private async Task<long> CreateOpportunityAsync(string token, string partySearch)
    {
        var partyId = await PartyIdAsync(token, partySearch);
        var response = await SendAsync(HttpMethod.Post, "/opportunities", token, new { partyId, currency = "TRY", estimatedAmount = 100m });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("opportunityId").GetInt64();
    }

    private async Task<(long Id, long Version)> CreateAsync(string token, object body)
    {
        var response = await SendAsync(HttpMethod.Post, Route, token, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await Json(response);
        return (created.GetProperty("id").GetInt64(), created.GetProperty("rowVersion").GetInt64());
    }

    private async Task<JsonElement> ReadAsync(string token, long id)
    {
        var response = await GetAsync($"{Route}/{id}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await Json(response);
    }

    private async Task<JsonElement> ListAsync(string token, DateOnly day)
    {
        var from = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(-2);
        var to = from.AddDays(8);
        var response = await GetAsync($"{Route}?from={Uri.EscapeDataString(Utc(from))}&to={Uri.EscapeDataString(Utc(to))}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await Json(response);
    }

    private Task<HttpResponseMessage> GetAsync(string path, string token) => SendAsync(HttpMethod.Get, path, token, key: null);

    /// <summary>Sends a request; a fresh idempotency key unless one is given, none when `key` is null.</summary>
    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string token, object? body = null, string? key = "<new>")
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (key is not null)
            request.Headers.TryAddWithoutValidation("Idempotency-Key", key == "<new>" ? Guid.NewGuid().ToString() : key);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return Client.SendAsync(request);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string type)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await Json(response);
        Assert.Equal(type, body.GetProperty("type").GetString());
    }
}
