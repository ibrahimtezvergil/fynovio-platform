using Collaboration.Application;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Collaboration.Tests.Integration;

/// <summary>D6 from the module's side: a link is validated through <see cref="ILinkTargetDirectory"/> before it is persisted,
/// hydrated from it (one batched call) when read, and its label is never stored. The directory is scripted; the real
/// resolvers are proven by CRM.Tests and Host.Tests.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class CalendarEntryLinkTests(PostgresFixture fixture)
{
    private readonly Harness _harness = new(fixture);

    private static EntityRef Opportunity(TenantId tenant, long id = 17) => new(tenant, "crm", "opportunity", id);

    // ---- write path ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_with_an_accessible_link_stores_the_reference_and_asks_the_directory_once_as_the_caller()
    {
        var tenant = TestData.NextTenant();
        var directory = StubLinkDirectory.AllowAll();

        var created = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k", link: Opportunity(tenant)), links: directory);

        Assert.Equal(1, directory.Calls);
        Assert.Equal([Opportunity(tenant)], directory.Requests[0]);
        Assert.Equal(Opportunity(tenant), (await _harness.ReadEntryAsync(tenant, created.Id))!.Link);
    }

    [Fact]
    public async Task Create_with_an_unavailable_link_is_rejected_and_nothing_is_written()
    {
        var tenant = TestData.NextTenant();

        await Assert.ThrowsAsync<CalendarLinkTargetUnavailableException>(() =>
            _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k", link: Opportunity(tenant)), links: StubLinkDirectory.DenyAll()));

        Assert.Equal(0, await _harness.CountEntriesAsync(tenant));
        Assert.Equal(0, await _harness.CountOutboxAsync(tenant));
        Assert.Equal(0, await _harness.CountIdempotencyAsync(tenant));
    }

    [Fact]
    public async Task Create_when_the_directory_fails_is_the_same_rejection_and_the_failure_text_does_not_escape()
    {
        var tenant = TestData.NextTenant();

        var ex = await Assert.ThrowsAsync<CalendarLinkTargetUnavailableException>(() =>
            _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k", link: Opportunity(tenant)), links: StubLinkDirectory.Failing()));

        Assert.DoesNotContain("SECRET", ex.Message);
        Assert.Null(ex.InnerException);
        Assert.Equal(0, await _harness.CountEntriesAsync(tenant));
    }

    [Fact]
    public async Task Create_when_the_directory_leaves_the_link_out_of_its_answer_is_rejected()
    {
        var tenant = TestData.NextTenant();
        var silent = new SilentDirectory();

        await Assert.ThrowsAsync<CalendarLinkTargetUnavailableException>(() =>
            _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k", link: Opportunity(tenant)), links: silent));
    }

    [Fact]
    public async Task Create_without_a_link_never_calls_the_directory()
    {
        var directory = StubLinkDirectory.Failing();

        await _harness.CreateAsync(Commands.Timed(TestData.NextTenant(), Harness.Alice, "k"), links: directory);

        Assert.Equal(0, directory.Calls);
    }

    [Fact]
    public async Task A_denied_caller_gets_the_authorization_error_before_the_directory_is_ever_asked()
    {
        var tenant = TestData.NextTenant();
        var directory = StubLinkDirectory.AllowAll();

        await Assert.ThrowsAsync<CalendarEntryAuthorizationDeniedException>(() =>
            _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k", link: Opportunity(tenant)), StubAuthorizer.AlwaysDeny, directory));

        Assert.Equal(0, directory.Calls);
    }

    [Fact]
    public async Task Replaying_a_create_with_a_link_succeeds_without_asking_the_directory_even_if_the_target_became_unavailable()
    {
        var tenant = TestData.NextTenant();
        var command = Commands.Timed(tenant, Harness.Alice, "k-replay", link: Opportunity(tenant));
        var first = await _harness.CreateAsync(command, links: StubLinkDirectory.AllowAll());
        var later = StubLinkDirectory.DenyAll();

        var replay = await _harness.CreateAsync(command, links: later);

        Assert.True(replay.Replayed);
        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(0, later.Calls);
        Assert.Equal(1, await _harness.CountEntriesAsync(tenant));
    }

    [Fact]
    public async Task Update_that_adds_a_link_validates_it()
    {
        var tenant = TestData.NextTenant();
        var created = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "seed"));

        await Assert.ThrowsAsync<CalendarLinkTargetUnavailableException>(() =>
            _harness.UpdateAsync(Commands.Update(tenant, created.Id, Harness.Alice, 1, "u1", link: Opportunity(tenant)), links: StubLinkDirectory.DenyAll()));
        Assert.Null((await _harness.ReadEntryAsync(tenant, created.Id))!.Link);

        var directory = StubLinkDirectory.AllowAll();
        await _harness.UpdateAsync(Commands.Update(tenant, created.Id, Harness.Alice, 1, "u2", link: Opportunity(tenant)), links: directory);

        Assert.Equal(1, directory.Calls);
        Assert.Equal(Opportunity(tenant), (await _harness.ReadEntryAsync(tenant, created.Id))!.Link);
    }

    [Fact]
    public async Task Update_that_changes_the_link_to_an_unavailable_target_is_rejected_and_keeps_the_old_link_and_version()
    {
        var tenant = TestData.NextTenant();
        var created = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "seed", link: Opportunity(tenant, 1)));

        await Assert.ThrowsAsync<CalendarLinkTargetUnavailableException>(() =>
            _harness.UpdateAsync(Commands.Update(tenant, created.Id, Harness.Alice, 1, "u", link: Opportunity(tenant, 2)), links: StubLinkDirectory.DenyAll()));

        var stored = (await _harness.ReadEntryAsync(tenant, created.Id))!;
        Assert.Equal(Opportunity(tenant, 1), stored.Link);
        Assert.Equal(1, stored.RowVersion);
    }

    [Fact]
    public async Task Update_that_keeps_the_stored_link_does_not_resolve_it_so_an_entry_whose_target_vanished_stays_editable()
    {
        var tenant = TestData.NextTenant();
        var created = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "seed", link: Opportunity(tenant)));
        var directory = StubLinkDirectory.DenyAll();

        var result = await _harness.UpdateAsync(
            Commands.Update(tenant, created.Id, Harness.Alice, 1, "u", title: "Renamed", link: Opportunity(tenant)), links: directory);

        Assert.Equal(2, result.RowVersion);
        Assert.Equal(0, directory.Calls);
        var stored = (await _harness.ReadEntryAsync(tenant, created.Id))!;
        Assert.Equal("Renamed", stored.Title);
        Assert.Equal(Opportunity(tenant), stored.Link);
    }

    [Fact]
    public async Task Update_that_clears_the_link_needs_no_directory_call()
    {
        var tenant = TestData.NextTenant();
        var created = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "seed", link: Opportunity(tenant)));
        var directory = StubLinkDirectory.Failing();

        await _harness.UpdateAsync(Commands.Update(tenant, created.Id, Harness.Alice, 1, "u", link: null), links: directory);

        Assert.Equal(0, directory.Calls);
        Assert.Null((await _harness.ReadEntryAsync(tenant, created.Id))!.Link);
    }

    [Fact]
    public async Task The_idempotency_hash_does_not_depend_on_the_directory_so_a_retry_of_a_kept_link_update_replays()
    {
        var tenant = TestData.NextTenant();
        var created = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "seed", link: Opportunity(tenant)));
        var command = Commands.Update(tenant, created.Id, Harness.Alice, 1, "u-same", title: "Renamed", link: Opportunity(tenant));

        var first = await _harness.UpdateAsync(command, links: StubLinkDirectory.DenyAll());
        var retry = await _harness.UpdateAsync(command, links: StubLinkDirectory.AllowAll());

        Assert.False(first.Replayed);
        Assert.True(retry.Replayed);
        Assert.Equal(first.RowVersion, retry.RowVersion);
        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() =>
            _harness.UpdateAsync(command with { Link = Opportunity(tenant, 99) }, links: StubLinkDirectory.AllowAll()));
    }

    // ---- read path ----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Get_hydrates_an_accessible_link_with_label_and_subtitle()
    {
        var tenant = TestData.NextTenant();
        var created = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k", link: Opportunity(tenant)));

        var dto = await _harness.GetAsync(tenant, created.Id, Harness.Alice, links: StubLinkDirectory.AllowAll());

        Assert.Equal(new CalendarEntryLinkDto(Opportunity(tenant), true, "Target opportunity 17", "Subtitle"), dto!.Link);
    }

    [Fact]
    public async Task Get_reports_an_unavailable_link_with_its_reference_and_no_label()
    {
        var tenant = TestData.NextTenant();
        var created = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k", link: Opportunity(tenant)));

        var dto = await _harness.GetAsync(tenant, created.Id, Harness.Alice, links: StubLinkDirectory.DenyAll());

        Assert.Equal(new CalendarEntryLinkDto(Opportunity(tenant), false), dto!.Link);
        Assert.Null(dto.Link!.Label);
        Assert.Null(dto.Link.Subtitle);
    }

    [Fact]
    public async Task Get_still_answers_when_the_directory_fails_and_reports_the_link_unavailable()
    {
        var tenant = TestData.NextTenant();
        var created = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k", link: Opportunity(tenant)));

        var dto = await _harness.GetAsync(tenant, created.Id, Harness.Alice, links: StubLinkDirectory.Failing());

        Assert.Equal("Entry", dto!.Title);
        Assert.False(dto.Link!.Accessible);
    }

    [Fact]
    public async Task Get_of_an_unlinked_entry_never_calls_the_directory()
    {
        var tenant = TestData.NextTenant();
        var created = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k"));
        var directory = StubLinkDirectory.Failing();

        var dto = await _harness.GetAsync(tenant, created.Id, Harness.Alice, links: directory);

        Assert.Null(dto!.Link);
        Assert.Equal(0, directory.Calls);
    }

    [Fact]
    public async Task Another_owners_linked_entry_is_not_found_and_its_link_is_never_resolved()
    {
        var tenant = TestData.NextTenant();
        var created = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k", link: Opportunity(tenant)));
        var directory = StubLinkDirectory.AllowAll();

        var dto = await _harness.GetAsync(tenant, created.Id, Harness.Bob, links: directory);
        var listed = await _harness.ListAsync(tenant, Harness.Bob, Commands.Noon.AddDays(-1), Commands.Noon.AddDays(1), links: directory);

        Assert.Null(dto);
        Assert.Empty(listed);
        Assert.Equal(0, directory.Calls);
    }

    [Fact]
    public async Task List_hydrates_every_link_with_exactly_one_batched_directory_call_over_the_distinct_references()
    {
        var tenant = TestData.NextTenant();
        foreach (var (key, link) in new (string, EntityRef?)[]
                 {
                     ("a", Opportunity(tenant, 1)), ("b", Opportunity(tenant, 2)), ("c", Opportunity(tenant, 1)),
                     ("d", new EntityRef(tenant, "masterdata", "party", 5)), ("e", null),
                 })
        {
            await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, key, title: key, link: link, start: Commands.Noon.AddHours(key[0] - 'a')));
        }

        var directory = new StubLinkDirectory(reference => reference.Id == 2
            ? LinkTargetResolution.Unavailable.Instance
            : new LinkTargetResolution.Accessible($"L{reference.Id}"));
        var listed = await _harness.ListAsync(tenant, Harness.Alice, Commands.Noon.AddDays(-1), Commands.Noon.AddDays(1), links: directory);

        Assert.Equal(1, directory.Calls);
        Assert.Equal(3, directory.Requests[0].Count);
        Assert.Equal(5, listed.Count);
        Assert.Equal(new string?[] { "L1", null, "L1", "L5", null }, listed.Select(e => e.Link?.Label));
        Assert.Equal([true, false, true, true], listed.Where(e => e.Link is not null).Select(e => e.Link!.Accessible).ToArray());
        Assert.Null(listed[4].Link);
    }

    [Fact]
    public async Task List_with_no_links_never_calls_the_directory_and_a_failing_directory_does_not_fail_the_page()
    {
        var tenant = TestData.NextTenant();
        await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "plain"));
        await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "linked", start: Commands.Noon.AddHours(1), link: Opportunity(tenant)));
        var none = StubLinkDirectory.AllowAll();

        var withFailure = await _harness.ListAsync(tenant, Harness.Alice, Commands.Noon.AddDays(-1), Commands.Noon.AddDays(1), links: StubLinkDirectory.Failing());
        var otherTenant = await _harness.ListAsync(TestData.NextTenant(), Harness.Alice, Commands.Noon.AddDays(-1), Commands.Noon.AddDays(1), links: none);

        Assert.Equal(2, withFailure.Count);
        Assert.False(withFailure[1].Link!.Accessible);
        Assert.Empty(otherTenant);
        Assert.Equal(0, none.Calls);
    }

    // ---- privacy ------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_resolved_label_is_never_persisted_in_the_entry_the_outbox_or_the_idempotency_record()
    {
        var tenant = TestData.NextTenant();
        var directory = new StubLinkDirectory(_ => new LinkTargetResolution.Accessible("SECRET-OPPORTUNITY-LABEL", "SECRET-SUBTITLE"));
        var created = await _harness.CreateAsync(Commands.Timed(tenant, Harness.Alice, "k", link: Opportunity(tenant)), links: directory);
        var current = await _harness.GetAsync(tenant, created.Id, Harness.Alice, links: directory);
        await _harness.UpdateAsync(Commands.Update(tenant, created.Id, Harness.Alice, 1, "u", link: Opportunity(tenant, 3)), links: directory);

        Assert.Equal("SECRET-OPPORTUNITY-LABEL", current!.Link!.Label); // the read did hydrate, so an absence below means something
        await using var admin = fixture.CreateAdminContext();
        var everything = string.Join(
            "|",
            (await admin.CalendarEntries.Where(e => e.TenantId == tenant).ToListAsync()).Select(e => $"{e.Title}|{e.Notes}|{e.Color}|{e.LinkBoundedContext}|{e.LinkEntityType}|{e.LinkEntityId}")
                .Concat((await admin.OutboxMessages.Where(m => m.TenantId == tenant).ToListAsync()).Select(m => $"{m.Payload}|{m.Subject}|{m.EventType}"))
                .Concat((await admin.IdempotencyRecords.Where(r => r.TenantId == tenant).ToListAsync()).Select(r => r.ResponsePayload)));
        Assert.DoesNotContain("SECRET", everything);
    }

    /// <summary>A misbehaving directory that answers, but omits the requested reference.</summary>
    private sealed class SilentDirectory : ILinkTargetDirectory
    {
        public Task<IReadOnlyDictionary<EntityRef, LinkTargetResolution>> ResolveAsync(
            ActorContext actor, IReadOnlyCollection<EntityRef> references, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<EntityRef, LinkTargetResolution>>(new Dictionary<EntityRef, LinkTargetResolution>());
    }
}
