using Contracts;
using Xunit;

namespace CRM.Tests.LinkTargets;

/// <summary>Contracts has no test project of its own; the directory is pure composition, so its unit tests live here
/// (CRM.Tests references Contracts and is where the resolvers it composes are proven).</summary>
public sealed class LinkTargetDirectoryTests
{
    private static readonly TenantId Tenant = new(7);
    private static readonly ActorContext Actor = new(Tenant, new PrincipalRef("https://idp.local", "actor"), Guid.NewGuid());

    private static EntityRef Ref(string context, string type, long id, TenantId? tenant = null) => new(tenant ?? Tenant, context, type, id);

    [Fact]
    public async Task Routes_each_reference_to_the_resolver_that_owns_its_key_in_one_batch_per_resolver()
    {
        var opportunities = new FakeResolver("crm", "opportunity", id => new LinkTargetResolution.Accessible($"opp {id}"));
        var parties = new FakeResolver("masterdata", "party", id => new LinkTargetResolution.Accessible($"party {id}", "sub"));
        var directory = new LinkTargetDirectory([opportunities, parties]);

        var result = await directory.ResolveAsync(Actor,
            [Ref("crm", "opportunity", 1), Ref("masterdata", "party", 9), Ref("crm", "opportunity", 2), Ref("crm", "opportunity", 1)]);

        Assert.Equal(3, result.Count);
        Assert.Equal(new LinkTargetResolution.Accessible("opp 1"), result[Ref("crm", "opportunity", 1)]);
        Assert.Equal(new LinkTargetResolution.Accessible("party 9", "sub"), result[Ref("masterdata", "party", 9)]);
        Assert.Single(opportunities.Calls);
        Assert.Equal([1L, 2L], opportunities.Calls[0].OrderBy(x => x));
        Assert.Single(parties.Calls);
    }

    [Fact]
    public async Task Unknown_key_and_foreign_tenant_are_unavailable_without_calling_any_resolver()
    {
        var resolver = new FakeResolver("crm", "opportunity", id => new LinkTargetResolution.Accessible("x"));
        var directory = new LinkTargetDirectory([resolver]);

        var unknown = Ref("crm", "invoice", 1);
        var foreign = Ref("crm", "opportunity", 1, new TenantId(8));
        var result = await directory.ResolveAsync(Actor, [unknown, foreign]);

        Assert.Equal(LinkTargetResolution.Unavailable.Instance, result[unknown]);
        Assert.Equal(LinkTargetResolution.Unavailable.Instance, result[foreign]);
        Assert.Empty(resolver.Calls);
    }

    [Fact]
    public async Task Every_unavailable_cause_yields_the_same_value()
    {
        // missing from the resolver's answer, explicit Unavailable, unknown key, foreign tenant, resolver fault
        var partial = new FakeResolver("crm", "opportunity", id => id == 1 ? LinkTargetResolution.Unavailable.Instance : null);
        var faulty = new FaultyResolver("masterdata", "party");
        var directory = new LinkTargetDirectory([partial, faulty]);

        var refs = new[]
        {
            Ref("crm", "opportunity", 1), Ref("crm", "opportunity", 2), Ref("crm", "nothing", 1),
            Ref("crm", "opportunity", 3, new TenantId(99)), Ref("masterdata", "party", 5)
        };
        var result = await directory.ResolveAsync(Actor, refs);

        Assert.Equal(refs.Length, result.Count);
        Assert.All(refs, r => Assert.Equal(LinkTargetResolution.Unavailable.Instance, result[r]));
        Assert.All(refs, r => Assert.IsType<LinkTargetResolution.Unavailable>(result[r]));
    }

    [Fact]
    public async Task A_faulty_resolver_degrades_only_its_own_references()
    {
        var good = new FakeResolver("crm", "opportunity", id => new LinkTargetResolution.Accessible("ok"));
        var directory = new LinkTargetDirectory([new FaultyResolver("masterdata", "party"), good]);

        var result = await directory.ResolveAsync(Actor, [Ref("masterdata", "party", 1), Ref("crm", "opportunity", 1)]);

        Assert.Equal(LinkTargetResolution.Unavailable.Instance, result[Ref("masterdata", "party", 1)]);
        Assert.IsType<LinkTargetResolution.Accessible>(result[Ref("crm", "opportunity", 1)]);
    }

    [Fact]
    public async Task Cancellation_propagates_instead_of_being_swallowed()
    {
        var directory = new LinkTargetDirectory([new FaultyResolver("crm", "opportunity", new OperationCanceledException())]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => directory.ResolveAsync(Actor, [Ref("crm", "opportunity", 1)]));
    }

    [Fact]
    public async Task Resolvers_are_called_sequentially_never_concurrently()
    {
        var gate = new ConcurrencyProbe();
        var directory = new LinkTargetDirectory(
        [
            new FakeResolver("crm", "opportunity", _ => new LinkTargetResolution.Accessible("a"), gate),
            new FakeResolver("masterdata", "party", _ => new LinkTargetResolution.Accessible("b"), gate)
        ]);

        await directory.ResolveAsync(Actor, [Ref("crm", "opportunity", 1), Ref("masterdata", "party", 1)]);

        Assert.Equal(1, gate.MaxConcurrent);
    }

    [Fact]
    public async Task Empty_input_yields_an_empty_result()
    {
        var result = await new LinkTargetDirectory([]).ResolveAsync(Actor, []);

        Assert.Empty(result);
    }

    [Fact]
    public void Duplicate_resolver_registration_fails_fast()
    {
        var first = new FakeResolver("crm", "opportunity", _ => null);
        var second = new FakeResolver("crm", "opportunity", _ => null);

        Assert.Throws<ArgumentException>(() => new LinkTargetDirectory([first, second]));
    }

    [Theory]
    [InlineData("CRM", "opportunity")]
    [InlineData("crm", "Opportunity")]
    [InlineData("1crm", "opportunity")]
    [InlineData("crm", "opp-ortunity")]
    [InlineData("crm", "")]
    [InlineData("crm\n", "opportunity")]
    [InlineData("crm", "opportunity\n")]
    public void Resolver_keys_must_match_the_identifier_grammar(string context, string type) =>
        Assert.Throws<ArgumentException>(() => new LinkTargetDirectory([new FakeResolver(context, type, _ => null)]));

    [Fact]
    public void Unavailable_instances_are_equal_and_accessible_requires_a_label()
    {
        Assert.Equal(new LinkTargetResolution.Unavailable(), LinkTargetResolution.Unavailable.Instance);
        Assert.Throws<ArgumentException>(() => new LinkTargetResolution.Accessible(" "));
        Assert.Null(new LinkTargetResolution.Accessible("label", "  ").Subtitle);
    }

    private sealed class ConcurrencyProbe
    {
        private int _current;
        public int MaxConcurrent { get; private set; }

        public async Task EnterAsync()
        {
            var now = Interlocked.Increment(ref _current);
            MaxConcurrent = Math.Max(MaxConcurrent, now);
            await Task.Delay(20);
            Interlocked.Decrement(ref _current);
        }
    }

    private sealed class FakeResolver(string context, string type, Func<long, LinkTargetResolution?> answer, ConcurrencyProbe? probe = null) : ILinkTargetResolver
    {
        public string BoundedContext => context;
        public string EntityType => type;
        public List<IReadOnlyCollection<long>> Calls { get; } = [];

        public async Task<IReadOnlyDictionary<long, LinkTargetResolution>> ResolveAsync(
            ActorContext actor, IReadOnlyCollection<long> ids, CancellationToken cancellationToken = default)
        {
            Calls.Add(ids);
            if (probe is not null)
                await probe.EnterAsync();

            var result = new Dictionary<long, LinkTargetResolution>();
            foreach (var id in ids)
                if (answer(id) is { } resolution)
                    result[id] = resolution;
            return result;
        }
    }

    private sealed class FaultyResolver(string context, string type, Exception? exception = null) : ILinkTargetResolver
    {
        public string BoundedContext => context;
        public string EntityType => type;

        public Task<IReadOnlyDictionary<long, LinkTargetResolution>> ResolveAsync(
            ActorContext actor, IReadOnlyCollection<long> ids, CancellationToken cancellationToken = default) =>
            throw (exception ?? new InvalidOperationException("boom"));
    }
}
