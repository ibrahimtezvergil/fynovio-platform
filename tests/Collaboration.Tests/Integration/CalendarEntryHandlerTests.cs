using Collaboration.Application;
using Collaboration.Domain;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Collaboration.Tests.Integration;

/// <summary>Create/get behavior through the unprivileged runtime role (so RLS is in force).</summary>
[Collection(nameof(PostgresCollection))]
public sealed class CalendarEntryHandlerTests(PostgresFixture fixture)
{
    private readonly Harness _harness = new(fixture);

    [Fact]
    public async Task A_timed_entry_round_trips_every_field_and_stores_the_owner()
    {
        var tenant = TestData.NextTenant();
        var link = new EntityRef(tenant, "crm", "opportunity", 42);
        var start = new DateTimeOffset(2026, 3, 10, 9, 30, 0, TimeSpan.Zero);
        var command = Commands.Timed(tenant, Harness.Alice, "k-1", title: "Kickoff", notes: "Bring slides",
            color: "#AABBCC", start: start, end: start.AddHours(1), link: link);

        var created = await _harness.CreateAsync(command);
        var dto = await _harness.GetAsync(tenant, created.Id, Harness.Alice);

        Assert.False(created.Replayed);
        Assert.True(created.Id > 0);
        Assert.Equal(1, created.RowVersion);
        Assert.NotNull(dto);
        Assert.Equal(created.Id, dto.Id);
        Assert.Equal(1, dto.RowVersion);
        Assert.Equal("Kickoff", dto.Title);
        Assert.Equal("Bring slides", dto.Notes);
        Assert.Equal("#aabbcc", dto.Color);
        Assert.False(dto.AllDay);
        Assert.Equal(start, dto.StartAt);
        Assert.Equal(start.AddHours(1), dto.EndAt);
        Assert.Null(dto.StartDate);
        Assert.Null(dto.EndDate);
        Assert.Equal(link, dto.Link);

        await using var admin = fixture.CreateAdminContext();
        var row = await admin.CalendarEntries.SingleAsync(e => e.Id == created.Id);
        Assert.Equal(tenant, row.TenantId);
        Assert.Equal(Harness.Alice, row.Owner);
    }

    [Fact]
    public async Task An_all_day_entry_round_trips_as_dates_only()
    {
        var tenant = TestData.NextTenant();
        var created = await _harness.CreateAsync(
            Commands.AllDay(tenant, Harness.Alice, "k-ad", new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 12)));

        var dto = await _harness.GetAsync(tenant, created.Id, Harness.Alice);

        Assert.NotNull(dto);
        Assert.True(dto.AllDay);
        Assert.Equal(new DateOnly(2026, 3, 10), dto.StartDate);
        Assert.Equal(new DateOnly(2026, 3, 12), dto.EndDate);
        Assert.Null(dto.StartAt);
        Assert.Null(dto.EndAt);
        Assert.Null(dto.Link);
    }

    [Fact]
    public async Task Times_written_with_an_offset_persist_the_same_instant_in_utc()
    {
        var tenant = TestData.NextTenant();
        var start = new DateTimeOffset(2026, 3, 10, 13, 0, 0, TimeSpan.FromHours(3));
        var end = new DateTimeOffset(2026, 3, 10, 15, 0, 0, TimeSpan.FromHours(3));

        var created = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-off", start: start, end: end));
        var dto = await _harness.GetAsync(tenant, created.Id, Harness.Alice);

        Assert.NotNull(dto);
        Assert.Equal(new DateTimeOffset(2026, 3, 10, 10, 0, 0, TimeSpan.Zero), dto.StartAt);
        Assert.Equal(new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero), dto.EndAt);
        Assert.Equal(TimeSpan.Zero, dto.StartAt!.Value.Offset);
        Assert.Equal(TimeSpan.Zero, dto.EndAt!.Value.Offset);
    }

    [Fact]
    public async Task An_invalid_request_writes_nothing_and_leaves_the_key_reusable()
    {
        var tenant = TestData.NextTenant();
        var invalid = Commands.Timed(tenant, Harness.Alice, "k-invalid", color: "not-a-colour");

        await Assert.ThrowsAsync<ArgumentException>(() => _harness.CreateAsync(invalid));

        Assert.Equal(0, await _harness.CountEntriesAsync(tenant));
        Assert.Equal(0, await _harness.CountOutboxAsync(tenant));
        Assert.Equal(0, await _harness.CountIdempotencyAsync(tenant));

        var corrected = await _harness.CreateAsync(invalid with { Color = "#123456" });
        Assert.False(corrected.Replayed);
    }

    [Fact]
    public async Task A_link_from_another_tenant_is_rejected_and_nothing_is_written()
    {
        var tenant = TestData.NextTenant();
        var foreignLink = new EntityRef(TestData.NextTenant(), "crm", "opportunity", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-foreign", link: foreignLink)));

        Assert.Equal(0, await _harness.CountEntriesAsync(tenant));
    }

    [Fact]
    public async Task A_denied_create_throws_the_typed_exception_and_writes_nothing()
    {
        var tenant = TestData.NextTenant();

        var ex = await Assert.ThrowsAsync<CalendarEntryAuthorizationDeniedException>(() =>
            _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-deny"), StubAuthorizer.AlwaysDeny));

        Assert.Equal("collaboration.calendar_entry.create", ex.ActionKey);
        Assert.Equal(AuthorizationDenialStage.Coarse, ex.DenialStage);
        Assert.Equal(0, await _harness.CountEntriesAsync(tenant));
        Assert.Equal(0, await _harness.CountIdempotencyAsync(tenant));
    }

    [Fact]
    public async Task Get_of_an_unknown_id_is_null()
    {
        var tenant = TestData.NextTenant();

        Assert.Null(await _harness.GetAsync(tenant, long.MaxValue, Harness.Alice));
    }

    [Fact]
    public async Task Get_by_another_principal_in_the_same_tenant_is_null_even_though_the_row_exists()
    {
        var tenant = TestData.NextTenant();
        var created = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-own"));

        Assert.Null(await _harness.GetAsync(tenant, created.Id, Harness.Bob));
        Assert.Equal(1, await _harness.CountEntriesAsync(tenant));
        Assert.NotNull(await _harness.GetAsync(tenant, created.Id, Harness.Alice));
    }

    [Fact]
    public async Task Get_from_another_tenant_is_null_even_for_the_same_principal()
    {
        var tenant = TestData.NextTenant();
        var created = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-tenant"));

        Assert.Null(await _harness.GetAsync(TestData.NextTenant(), created.Id, Harness.Alice));
    }

    [Fact]
    public async Task Get_with_a_record_level_denial_is_indistinguishable_from_not_found()
    {
        var tenant = TestData.NextTenant();
        var created = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-rec"));

        Assert.Null(await _harness.GetAsync(tenant, created.Id, Harness.Alice, StubAuthorizer.RecordDenied));
        Assert.Null(await _harness.GetAsync(tenant, created.Id, Harness.Alice, StubAuthorizer.AlwaysDeny));
    }

    [Fact]
    public async Task Get_authorizes_with_the_entry_owner_as_the_resource_owner()
    {
        var tenant = TestData.NextTenant();
        var created = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-res"));
        var authorizer = new RecordingAuthorizer();

        await _harness.GetAsync(tenant, created.Id, Harness.Alice, authorizer);

        var request = Assert.Single(authorizer.Requests);
        Assert.Equal("collaboration.calendar_entry.read", request.Action.Value);
        Assert.Equal(created.Id, request.Resource.Id);
        Assert.Equal(Harness.Alice, request.Resource.OwnerPrincipal);
        Assert.Equal(Harness.Alice, request.Actor.Principal);
    }

    [Fact]
    public async Task Create_authorizes_with_a_create_shaped_resource_owned_by_the_actor()
    {
        var tenant = TestData.NextTenant();
        var authorizer = new RecordingAuthorizer();

        await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k-cr"), authorizer);

        var request = Assert.Single(authorizer.Requests);
        Assert.Equal("collaboration.calendar_entry.create", request.Action.Value);
        Assert.Null(request.Resource.Id);
        Assert.Equal(Harness.Alice, request.Resource.OwnerPrincipal);
    }

    private sealed class RecordingAuthorizer : IAuthorizer
    {
        public List<AuthorizationRequest> Requests { get; } = [];

        public Task<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return StubAuthorizer.AlwaysAllow.AuthorizeAsync(request, cancellationToken);
        }
    }
}
