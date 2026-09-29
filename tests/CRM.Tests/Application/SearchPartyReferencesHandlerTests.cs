using Contracts;
using CRM.Application;
using Xunit;

namespace CRM.Tests.Application;

public sealed class SearchPartyReferencesHandlerTests
{
    private static readonly TenantId Tenant = new(7);

    private sealed class FakeParties(params PartyDirectoryEntry[] entries) : IPartySearch, IPartyDirectory
    {
        public List<(string? Query, int Take)> Searches { get; } = [];
        public List<IReadOnlyCollection<PartyRef>> Lookups { get; } = [];

        public Task<IReadOnlyList<PartyDirectoryEntry>> SearchPartiesAsync(TenantId tenantId, string? query, int take, CancellationToken cancellationToken = default)
        {
            Searches.Add((query, take));
            return Task.FromResult<IReadOnlyList<PartyDirectoryEntry>>(entries);
        }

        public Task<PartyDirectoryEntry?> GetPartyAsync(PartyRef partyRef, CancellationToken cancellationToken = default) =>
            Task.FromResult(entries.FirstOrDefault(e => e.PartyRef == partyRef));

        public Task<IReadOnlyDictionary<PartyRef, PartyDirectoryEntry>> GetPartiesAsync(IReadOnlyCollection<PartyRef> partyRefs, CancellationToken cancellationToken = default)
        {
            Lookups.Add(partyRefs);
            return Task.FromResult<IReadOnlyDictionary<PartyRef, PartyDirectoryEntry>>(
                entries.Where(e => partyRefs.Contains(e.PartyRef)).ToDictionary(e => e.PartyRef));
        }
    }

    private static PartyDirectoryEntry Entry(long id, string name, string? surname = null, string? email = null, PartyType type = PartyType.Organization, string? phone = null) =>
        new(new PartyRef(Tenant, id), type, name, surname, email, phone);

    private static SearchPartyReferencesQuery Query(string? search = null, IReadOnlyCollection<long>? ids = null, int take = 10) =>
        new(Tenant, TestData.Seller, search, ids, take, Guid.NewGuid());

    [Fact]
    public async Task Search_returns_display_fields_only()
    {
        var parties = new FakeParties(Entry(1, "Acme", email: "a@acme.test"), Entry(2, "Ada", "Lovelace", type: PartyType.Person));
        var handler = new SearchPartyReferencesHandler(StubAuthorizer.AlwaysAllow, parties, parties);

        var result = await handler.HandleAsync(Query("a", take: 5));

        Assert.Equal([new PartyReferenceDto(1, "Organization", "Acme", "a@acme.test"), new PartyReferenceDto(2, "Person", "Ada Lovelace", null)], result);
        Assert.Equal(("a", 5), Assert.Single(parties.Searches));
        Assert.Empty(parties.Lookups);
    }

    [Fact]
    public async Task Search_carries_the_phone_so_the_picker_can_show_it()
    {
        var parties = new FakeParties(Entry(1, "Acme", phone: "+90 532 111 22 33"));
        var handler = new SearchPartyReferencesHandler(StubAuthorizer.AlwaysAllow, parties, parties);

        var result = await handler.HandleAsync(Query("acme"));

        Assert.Equal("+90 532 111 22 33", Assert.Single(result).Phone);
    }

    [Fact]
    public async Task Is_gated_by_the_party_search_action_with_no_resource()
    {
        var authorizer = new RecordingAuthorizer();
        var parties = new FakeParties();

        await new SearchPartyReferencesHandler(authorizer, parties, parties).HandleAsync(Query("x"));

        Assert.Equal(["crm.reference.party.search"], authorizer.Actions);
    }

    [Fact]
    public async Task A_denied_actor_gets_a_coarse_denial_and_MasterData_is_never_asked()
    {
        var parties = new FakeParties(Entry(1, "Acme"));
        var handler = new SearchPartyReferencesHandler(StubAuthorizer.AlwaysDeny, parties, parties);

        var denied = await Assert.ThrowsAsync<OpportunityAuthorizationDeniedException>(() => handler.HandleAsync(Query("a")));

        Assert.Equal(AuthorizationDenialStage.Coarse, denied.DenialStage);
        Assert.Null(denied.OpportunityId);
        Assert.Empty(parties.Searches);
        Assert.Empty(parties.Lookups);
    }

    [Fact]
    public async Task Ids_take_precedence_and_unknown_ids_are_simply_absent()
    {
        var parties = new FakeParties(Entry(1, "Acme"), Entry(2, "Globex"));
        var handler = new SearchPartyReferencesHandler(StubAuthorizer.AlwaysAllow, parties, parties);

        var result = await handler.HandleAsync(Query(search: "ignored", ids: [2, 99, 2]));

        Assert.Equal([2L], result.Select(r => r.Id));
        Assert.Empty(parties.Searches);
        Assert.Equal([2L, 99L], Assert.Single(parties.Lookups).Select(r => r.PartyId)); // de-duplicated
        Assert.All(Assert.Single(parties.Lookups), r => Assert.Equal(Tenant, r.TenantId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task A_non_positive_id_is_a_validation_error(long id)
    {
        var parties = new FakeParties();
        var handler = new SearchPartyReferencesHandler(StubAuthorizer.AlwaysAllow, parties, parties);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(Query(ids: [1, id])));
    }

    [Fact]
    public async Task More_than_the_maximum_ids_is_a_validation_error()
    {
        var parties = new FakeParties();
        var handler = new SearchPartyReferencesHandler(StubAuthorizer.AlwaysAllow, parties, parties);
        var tooMany = Enumerable.Range(1, SearchPartyReferencesHandler.MaxIds + 1).Select(i => (long)i).ToList();

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(Query(ids: tooMany)));
    }

    [Fact]
    public async Task A_missing_take_falls_back_to_the_default()
    {
        var parties = new FakeParties();
        var handler = new SearchPartyReferencesHandler(StubAuthorizer.AlwaysAllow, parties, parties);

        await handler.HandleAsync(Query("x", take: 0));

        Assert.Equal(SearchPartyReferencesHandler.DefaultTake, Assert.Single(parties.Searches).Take);
    }
}
