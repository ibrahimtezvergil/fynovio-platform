using System.Data.Common;
using Access.Application;
using Access.Domain.Authorization;
using Access.Domain.Identity;
using Access.Persistence;
using Contracts;
using CRM.Application;
using CRM.Domain;
using CRM.Persistence;
using CRM.Tests.Integration;
using MasterData.Application;
using MasterData.Domain;
using MasterData.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace CRM.Tests.LinkTargets;

/// <summary>Both CRM link resolvers against real PostgreSQL, the real Access PDP and the unprivileged runtime role
/// (RLS is inert on the migration superuser, so anything else would prove nothing).</summary>
[Collection(nameof(PostgresCollection))]
public sealed class CrmLinkTargetResolverTests
{
    private const string OpportunityRead = "crm.opportunity.read";
    private const string PartySearch = "crm.reference.party.search";

    private readonly PostgresFixture _fixture;

    private readonly ITestOutputHelper _output;

    public CrmLinkTargetResolverTests(PostgresFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // ---- opportunity resolver: authorization agreement -------------------------------------------------------

    /// <summary>The resolver must answer exactly as GetOpportunityHandler does, for every kind of actor. The handler
    /// asks the single-resource PDP, the resolver asks the collection scope resolver; this matrix is where any drift
    /// between the two would show.</summary>
    [Fact]
    public async Task Opportunity_resolver_agrees_with_the_read_handler_for_every_kind_of_actor()
    {
        var owner = Fresh("owner");
        var (tenant, opportunityId) = await SeedOpportunityAsync(owner, "Acme");
        var otherTenant = TestData.NextTenant();

        var ownerOnlyGrant = Fresh("owner-only");   // owner-relation grant, does NOT own the record
        var tenantWide = Fresh("tenant-wide");
        var noGrant = Fresh("no-grant");
        var stranger = Fresh("stranger");           // no account at all
        var foreignTenantActor = Fresh("foreign");  // tenant-wide grant, but in another tenant

        await GrantAsync(tenant, owner, PermissionSetItem.OwnerRelation, (OpportunityRead, nameof(Opportunity)));
        await GrantAsync(tenant, ownerOnlyGrant, PermissionSetItem.OwnerRelation, (OpportunityRead, nameof(Opportunity)));
        await GrantAsync(tenant, tenantWide, null, (OpportunityRead, nameof(Opportunity)));
        await EnrollWithoutGrantsAsync(tenant, noGrant);
        await GrantAsync(otherTenant, foreignTenantActor, null, (OpportunityRead, nameof(Opportunity)));

        var cases = new (string Name, TenantId Tenant, PrincipalRef Principal, bool ExpectAccessible)[]
        {
            ("owner with owner-relation grant", tenant, owner, true),
            ("tenant-wide grant", tenant, tenantWide, true),
            ("owner-relation grant on someone else's record", tenant, ownerOnlyGrant, false),
            ("member with no grant", tenant, noGrant, false),
            ("unrecognized principal", tenant, stranger, false),
            ("tenant-wide grant in another tenant", otherTenant, foreignTenantActor, false),
        };

        foreach (var (name, actorTenant, principal, expectAccessible) in cases)
        {
            await using var h = await Harness.CreateAsync(_fixture);
            var actor = new ActorContext(actorTenant, principal, Guid.NewGuid());

            var viaHandler = await new GetOpportunityHandler(h.Crm, h.Authorizer, StubDefinitionReader.None, StubLinkTargetDirectory.None)
                .HandleAsync(new GetOpportunityQuery(actorTenant, opportunityId, principal, Guid.NewGuid()));
            var viaResolver = (await h.OpportunityResolver.ResolveAsync(actor, [opportunityId])).GetValueOrDefault(opportunityId);

            Assert.True((viaHandler is not null) == expectAccessible, $"{name}: handler disagreed with the expectation");
            Assert.True((viaResolver is LinkTargetResolution.Accessible) == expectAccessible, $"{name}: resolver disagreed with the expectation");
        }
    }

    [Fact]
    public async Task Every_reason_an_opportunity_is_not_shown_is_the_same_answer_through_the_directory()
    {
        var owner = Fresh("owner");
        var (tenant, opportunityId) = await SeedOpportunityAsync(owner, "Acme");
        var (_, foreignOpportunityId) = await SeedOpportunityAsync(Fresh("someone"), "Elsewhere");
        var viewer = Fresh("viewer");
        await GrantAsync(tenant, viewer, PermissionSetItem.OwnerRelation, (OpportunityRead, nameof(Opportunity)));

        await using var h = await Harness.CreateAsync(_fixture);
        var actor = new ActorContext(tenant, viewer, Guid.NewGuid());
        var refs = new[]
        {
            new EntityRef(tenant, "crm", "opportunity", opportunityId),           // exists, denied (not the owner)
            new EntityRef(tenant, "crm", "opportunity", 999_999_999),             // does not exist
            new EntityRef(tenant, "crm", "opportunity", foreignOpportunityId),    // exists, but in another tenant
            new EntityRef(tenant, "crm", "widget", opportunityId),                // unknown entity type
        };

        var result = await h.Directory.ResolveAsync(actor, refs);

        Assert.Equal(refs.Length, result.Count);
        Assert.All(refs, r => Assert.Equal(LinkTargetResolution.Unavailable.Instance, result[r]));
    }

    [Fact]
    public async Task Opportunity_labels_carry_the_party_name_and_stage_only_for_actors_who_may_read_parties()
    {
        var actorWithParties = Fresh("with-parties");
        var actorWithoutParties = Fresh("without-parties");
        var (tenant, opportunityId, stageName) = await SeedOpportunityWithStageAsync(actorWithParties, "Contoso Ltd", "Teklif Verildi");
        await GrantAsync(tenant, actorWithParties, null, (OpportunityRead, nameof(Opportunity)), (PartySearch, "PartyReference"));
        await GrantAsync(tenant, actorWithoutParties, null, (OpportunityRead, nameof(Opportunity)));

        await using var h = await Harness.CreateAsync(_fixture);

        var withParties = (await h.OpportunityResolver.ResolveAsync(new ActorContext(tenant, actorWithParties, Guid.NewGuid()), [opportunityId]))[opportunityId];
        var withoutParties = (await h.OpportunityResolver.ResolveAsync(new ActorContext(tenant, actorWithoutParties, Guid.NewGuid()), [opportunityId]))[opportunityId];

        Assert.Equal(new LinkTargetResolution.Accessible($"#{opportunityId} · Contoso Ltd", stageName), withParties);
        var plain = Assert.IsType<LinkTargetResolution.Accessible>(withoutParties);
        Assert.Equal($"#{opportunityId}", plain.Label);
        Assert.DoesNotContain("Contoso", plain.Label);
        Assert.Equal(stageName, plain.Subtitle);
    }

    [Fact]
    public async Task A_merged_party_is_labelled_with_its_survivor_never_the_tombstone()
    {
        var owner = Fresh("owner");
        var tenant = TestData.NextTenant();
        await using var masterData = _fixture.CreateMasterDataContext();
        var survivor = Party.Create(tenant, PartyType.Organization, "Survivor AS", null, null, null);
        var tombstone = Party.Create(tenant, PartyType.Organization, "Old Name AS", null, null, null);
        masterData.Parties.AddRange(survivor, tombstone);
        await masterData.SaveChangesAsync();
        tombstone.MergeInto(survivor);
        await masterData.SaveChangesAsync();

        await using var seed = _fixture.CreateAdminContext();
        var opportunity = Opportunity.Create(tenant, new PartyRef(tenant, tombstone.Id), owner, "TRY", 10m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();
        await GrantAsync(tenant, owner, null, (OpportunityRead, nameof(Opportunity)), (PartySearch, "PartyReference"));

        await using var h = await Harness.CreateAsync(_fixture);
        var actor = new ActorContext(tenant, owner, Guid.NewGuid());

        var viaOpportunity = (await h.OpportunityResolver.ResolveAsync(actor, [opportunity.Id]))[opportunity.Id];
        var viaParty = (await h.PartyResolver.ResolveAsync(actor, [tombstone.Id, survivor.Id]));

        Assert.Equal($"#{opportunity.Id} · Survivor AS", Assert.IsType<LinkTargetResolution.Accessible>(viaOpportunity).Label);
        Assert.Equal("Survivor AS", Assert.IsType<LinkTargetResolution.Accessible>(viaParty[tombstone.Id]).Label);
        Assert.Equal("Survivor AS", Assert.IsType<LinkTargetResolution.Accessible>(viaParty[survivor.Id]).Label);
    }

    // ---- opportunity resolver: batching -----------------------------------------------------------------------

    [Fact]
    public async Task A_mixed_batch_returns_accessible_and_omits_the_rest()
    {
        var me = Fresh("me");
        var tenant = TestData.NextTenant();
        var mine = new List<long>();
        for (var i = 0; i < 3; i++)
            mine.Add((await SeedOpportunityInAsync(tenant, me, $"Mine {i}")).Id);
        var theirs = (await SeedOpportunityInAsync(tenant, Fresh("them"), "Theirs")).Id;
        await GrantAsync(tenant, me, PermissionSetItem.OwnerRelation, (OpportunityRead, nameof(Opportunity)));

        await using var h = await Harness.CreateAsync(_fixture);
        var result = await h.OpportunityResolver.ResolveAsync(new ActorContext(tenant, me, Guid.NewGuid()), [.. mine, theirs, 424242, mine[0], -5]);

        Assert.Equal(mine.Order(), result.Keys.Order());
        Assert.All(result.Values, v => Assert.IsType<LinkTargetResolution.Accessible>(v));
    }

    [Fact]
    public async Task Resolving_many_opportunities_costs_the_same_number_of_crm_commands_as_resolving_one()
    {
        var me = Fresh("me");
        var tenant = TestData.NextTenant();
        var ids = new List<long>();
        for (var i = 0; i < 25; i++)
            ids.Add((await SeedOpportunityInAsync(tenant, me, $"Party {i}")).Id);
        await GrantAsync(tenant, me, null, (OpportunityRead, nameof(Opportunity)), (PartySearch, "PartyReference"));
        var actor = new ActorContext(tenant, me, Guid.NewGuid());

        var one = new CommandCounter();
        await using (var h = await Harness.CreateAsync(_fixture, one))
            await h.OpportunityResolver.ResolveAsync(actor, [ids[0]]);

        var many = new CommandCounter();
        await using (var h = await Harness.CreateAsync(_fixture, many))
        {
            var result = await h.OpportunityResolver.ResolveAsync(actor, ids);
            Assert.Equal(25, result.Count);
        }

        Assert.Equal(one.Count, many.Count);
        Assert.InRange(many.Count, 1, 4); // set_config + opportunities (+ stages when any is assigned)
    }

    /// <summary>The authorization cost is deliberately per record (the exact decision the read handler makes), so the
    /// Access side is linear in the number of opportunities — this pins that it is linear and constant per record, not
    /// something worse, and records the figures.</summary>
    [Fact]
    public async Task Pdp_cost_is_constant_per_authorized_record_and_linear_in_the_batch()
    {
        var me = Fresh("me");
        var tenant = TestData.NextTenant();
        var ids = new List<long>();
        for (var i = 0; i < 25; i++)
            ids.Add((await SeedOpportunityInAsync(tenant, me, $"Party {i}")).Id);
        await GrantAsync(tenant, me, null, (OpportunityRead, nameof(Opportunity)), (PartySearch, "PartyReference"));
        var actor = new ActorContext(tenant, me, Guid.NewGuid());

        async Task<int> AccessCommandsAsync(int take)
        {
            var counter = new CommandCounter();
            await using var h = await Harness.CreateAsync(_fixture, accessInterceptor: counter);
            var result = await h.OpportunityResolver.ResolveAsync(actor, ids.Take(take).ToList());
            Assert.Equal(take, result.Count);
            return counter.Count;
        }

        var one = await AccessCommandsAsync(1);
        var two = await AccessCommandsAsync(2);
        var twentyFive = await AccessCommandsAsync(25);
        var perRecord = two - one;
        _output.WriteLine($"Access commands: 1 record={one}, 2 records={two}, 25 records={twentyFive}, per extra record={perRecord}");

        Assert.True(perRecord > 0);
        Assert.Equal(one + 24 * perRecord, twentyFive);
    }

    // ---- opportunity resolver: request identity, degradation ----------------------------------------------------

    /// <summary>The strongest drift guard: whatever the read handler asks the PDP about an opportunity, the resolver
    /// asks the identical question (action, resource type, id, owner, actor).</summary>
    [Fact]
    public async Task Opportunity_resolver_asks_the_pdp_exactly_what_the_read_handler_asks()
    {
        var owner = Fresh("owner");
        var (tenant, opportunityId) = await SeedOpportunityAsync(owner, "Acme");
        var actor = new ActorContext(tenant, Fresh("viewer"), Guid.NewGuid());

        await using var h = await Harness.CreateAsync(_fixture);
        var viaHandler = new RecordingAuthorizer();
        await new GetOpportunityHandler(h.Crm, viaHandler, StubDefinitionReader.None, StubLinkTargetDirectory.None)
            .HandleAsync(new GetOpportunityQuery(tenant, opportunityId, actor.Principal, actor.CorrelationId));
        var viaResolver = new RecordingAuthorizer();
        await new OpportunityLinkTargetResolver(h.Crm, viaResolver, new ThrowingPartyDirectory())
            .ResolveAsync(actor, [opportunityId]);

        var handlerRequest = Assert.Single(viaHandler.Requests);
        var resolverRequest = viaResolver.Requests.Single(r => r.Action.Value == OpportunityRead);
        Assert.Equal(handlerRequest, resolverRequest);
        Assert.Equal(OpportunityRead, handlerRequest.Action.Value);
        Assert.Equal(nameof(Opportunity), handlerRequest.Resource.ResourceType);
        Assert.Equal(opportunityId, handlerRequest.Resource.Id);
        Assert.Equal(owner, handlerRequest.Resource.OwnerPrincipal);
    }

    [Fact]
    public async Task A_failing_party_lookup_degrades_the_label_and_never_the_link()
    {
        var owner = Fresh("owner");
        var (tenant, opportunityId) = await SeedOpportunityAsync(owner, "Secret Party");
        await using var h = await Harness.CreateAsync(_fixture);
        var resolver = new OpportunityLinkTargetResolver(h.Crm, StubAuthorizer.AlwaysAllow, new ThrowingPartyDirectory());

        var result = await resolver.ResolveAsync(new ActorContext(tenant, owner, Guid.NewGuid()), [opportunityId]);

        Assert.Equal(new LinkTargetResolution.Accessible($"#{opportunityId}"), result[opportunityId]);
    }

    [Fact]
    public async Task Parties_are_looked_up_once_and_only_for_opportunities_the_actor_may_see()
    {
        var me = Fresh("me");
        var tenant = TestData.NextTenant();
        var mine = await SeedOpportunityInAsync(tenant, me, "Mine Ltd");
        var theirs = await SeedOpportunityInAsync(tenant, Fresh("them"), "Theirs Ltd");
        await GrantAsync(tenant, me, PermissionSetItem.OwnerRelation, (OpportunityRead, nameof(Opportunity)));

        await using var h = await Harness.CreateAsync(_fixture);
        var directory = new RecordingPartyDirectory(new PartyDirectory(h.MasterData));
        // The actor holds no party gate here, so the directory must not be consulted at all…
        var gated = new OpportunityLinkTargetResolver(h.Crm, h.Authorizer, directory);
        var result = await gated.ResolveAsync(new ActorContext(tenant, me, Guid.NewGuid()), [mine.Id, theirs.Id]);
        Assert.Equal(new LinkTargetResolution.Accessible($"#{mine.Id}"), Assert.Single(result).Value);
        Assert.Empty(directory.Requested);

        // …and once the gate is open, exactly one batch containing only the visible opportunity's party.
        var opened = new OpportunityLinkTargetResolver(h.Crm, new OpportunityReadOnlyAllowedAuthorizer(h.Authorizer), directory);
        var withParty = await opened.ResolveAsync(new ActorContext(tenant, me, Guid.NewGuid()), [mine.Id, theirs.Id]);
        Assert.Equal(new LinkTargetResolution.Accessible($"#{mine.Id} · Mine Ltd"), Assert.Single(withParty).Value);
        var batch = Assert.Single(directory.Requested);
        Assert.Equal([mine.PartyRefPartyId], batch);
    }

    [Fact]
    public async Task A_faulting_pdp_makes_the_directory_answer_unavailable_not_throw()
    {
        var owner = Fresh("owner");
        var (tenant, opportunityId) = await SeedOpportunityAsync(owner, "Acme");
        await using var h = await Harness.CreateAsync(_fixture);
        var resolver = new OpportunityLinkTargetResolver(h.Crm, new ThrowingAuthorizer(), new ThrowingPartyDirectory());
        var directory = new LinkTargetDirectory([resolver]);
        var reference = new EntityRef(tenant, "crm", "opportunity", opportunityId);

        var result = await directory.ResolveAsync(new ActorContext(tenant, owner, Guid.NewGuid()), [reference]);

        Assert.Equal(LinkTargetResolution.Unavailable.Instance, result[reference]);
    }

    [Fact]
    public async Task A_failing_party_directory_makes_a_party_link_unavailable_through_the_directory()
    {
        var tenant = TestData.NextTenant();
        var reference = new EntityRef(tenant, "masterdata", "party", 1);
        var directory = new LinkTargetDirectory([new PartyLinkTargetResolver(StubAuthorizer.AlwaysAllow, new ThrowingPartyDirectory())]);

        var result = await directory.ResolveAsync(new ActorContext(tenant, Fresh("a"), Guid.NewGuid()), [reference]);

        Assert.Equal(LinkTargetResolution.Unavailable.Instance, result[reference]);
    }

    // ---- party resolver ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Party_resolver_requires_the_party_gate_and_the_tenant()
    {
        var tenant = TestData.NextTenant();
        await using var masterData = _fixture.CreateMasterDataContext();
        var party = await TestData.CreatePartyAsync(masterData, tenant, "Fabrikam Inc");
        var person = Party.Create(tenant, PartyType.Person, "Ada", "Lovelace", null, null);
        masterData.Parties.Add(person);
        await masterData.SaveChangesAsync();
        var allowed = Fresh("allowed");
        var denied = Fresh("denied");
        var foreign = Fresh("foreign");
        var otherTenant = TestData.NextTenant();
        await GrantAsync(tenant, allowed, null, (PartySearch, "PartyReference"));
        await EnrollWithoutGrantsAsync(tenant, denied);
        await GrantAsync(otherTenant, foreign, null, (PartySearch, "PartyReference"));

        await using var h = await Harness.CreateAsync(_fixture);
        var directory = h.Directory;

        var ok = await directory.ResolveAsync(new ActorContext(tenant, allowed, Guid.NewGuid()), [new EntityRef(tenant, "masterdata", "party", party.PartyId)]);
        var notGated = await directory.ResolveAsync(new ActorContext(tenant, denied, Guid.NewGuid()), [new EntityRef(tenant, "masterdata", "party", party.PartyId)]);
        var otherTenantAsks = await directory.ResolveAsync(new ActorContext(otherTenant, foreign, Guid.NewGuid()), [new EntityRef(otherTenant, "masterdata", "party", party.PartyId)]);
        var missing = await directory.ResolveAsync(new ActorContext(tenant, allowed, Guid.NewGuid()), [new EntityRef(tenant, "masterdata", "party", 987_654_321)]);

        var accessible = Assert.IsType<LinkTargetResolution.Accessible>(Assert.Single(ok.Values));
        Assert.Equal("Fabrikam Inc", accessible.Label);
        Assert.Null(accessible.Subtitle);
        var personResult = await directory.ResolveAsync(new ActorContext(tenant, allowed, Guid.NewGuid()), [new EntityRef(tenant, "masterdata", "party", person.Id)]);
        Assert.Equal("Ada Lovelace", Assert.IsType<LinkTargetResolution.Accessible>(Assert.Single(personResult.Values)).Label);
        Assert.Equal(LinkTargetResolution.Unavailable.Instance, Assert.Single(notGated.Values));
        Assert.Equal(LinkTargetResolution.Unavailable.Instance, Assert.Single(otherTenantAsks.Values));
        Assert.Equal(LinkTargetResolution.Unavailable.Instance, Assert.Single(missing.Values));
    }

    // ---- helpers ----------------------------------------------------------------------------------------------

    private static PrincipalRef Fresh(string name) => new("https://idp.local", $"link-{name}-{Guid.NewGuid():N}");

    private async Task<(TenantId Tenant, long OpportunityId)> SeedOpportunityAsync(PrincipalRef owner, string partyName)
    {
        var tenant = TestData.NextTenant();
        var opportunity = await SeedOpportunityInAsync(tenant, owner, partyName);
        return (tenant, opportunity.Id);
    }

    private async Task<Opportunity> SeedOpportunityInAsync(TenantId tenant, PrincipalRef owner, string partyName)
    {
        await using var masterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(masterData, tenant, partyName);
        await using var crm = _fixture.CreateAdminContext();
        var opportunity = Opportunity.Create(tenant, partyRef, owner, "TRY", 100m);
        crm.Opportunities.Add(opportunity);
        await crm.SaveChangesAsync();
        return opportunity;
    }

    private async Task<(TenantId Tenant, long OpportunityId, string StageName)> SeedOpportunityWithStageAsync(PrincipalRef owner, string partyName, string stageName)
    {
        var tenant = TestData.NextTenant();
        await using var masterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(masterData, tenant, partyName);

        await using var crm = _fixture.CreateAdminContext();
        var definition = PipelineDefinition.Create(tenant, "Sales");
        crm.PipelineDefinitions.Add(definition);
        await crm.SaveChangesAsync();
        var version = definition.AddVersion(1);
        crm.PipelineDefinitionVersions.Add(version);
        await crm.SaveChangesAsync();
        var stage = version.AddStage(stageName, 0);
        crm.PipelineStages.Add(stage);
        await crm.SaveChangesAsync();

        var opportunity = Opportunity.Create(tenant, partyRef, owner, "TRY", 100m);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), version.Id, stage.Id);
        crm.Opportunities.Add(opportunity);
        await crm.SaveChangesAsync();
        return (tenant, opportunity.Id, stageName);
    }

    /// <summary>Recognized tenant member with zero grants (Account + ExternalIdentity + membership).</summary>
    private async Task EnrollWithoutGrantsAsync(TenantId tenant, PrincipalRef principal)
    {
        await using var access = _fixture.CreateAccessContext();
        var account = Account.Create($"{principal.Subject}@test.local", principal.Subject);
        access.Accounts.Add(account);
        await access.SaveChangesAsync();
        access.ExternalIdentities.Add(ExternalIdentity.Link(account.Id, principal));
        access.TenantMemberships.Add(TenantMembership.Invite(tenant, account.Id));
        if (!await access.TenantAccessStates.AnyAsync(s => s.TenantId == tenant))
            access.TenantAccessStates.Add(TenantAccessState.Initialize(tenant));
        await access.SaveChangesAsync();
        await EnsureActionsRegisteredAsync(access, (OpportunityRead, nameof(Opportunity)), (PartySearch, "PartyReference"));
    }

    private async Task GrantAsync(TenantId tenant, PrincipalRef principal, string? relation, params (string Key, string ResourceType)[] actions)
    {
        await using var access = _fixture.CreateAccessContext();
        var account = Account.Create($"{principal.Subject}@test.local", principal.Subject);
        access.Accounts.Add(account);
        await access.SaveChangesAsync();
        access.ExternalIdentities.Add(ExternalIdentity.Link(account.Id, principal));
        access.TenantMemberships.Add(TenantMembership.Invite(tenant, account.Id));
        await EnsureActionsRegisteredAsync(access, actions);

        var suffix = "t" + Guid.NewGuid().ToString("N")[..7];
        var permissionSet = PermissionSet.Create(tenant, $"link_test_permission_set_{suffix}", "Link Test Permission Set");
        foreach (var (key, _) in actions)
            permissionSet.Grant(key, relation);
        access.PermissionSets.Add(permissionSet);

        var role = Role.Create(tenant, $"link_test_role_{suffix}", $"Link Test Role {suffix}");
        access.Roles.Add(role);
        await access.SaveChangesAsync();

        access.RolePermissionSets.Add(RolePermissionSet.Create(tenant, role.Id, permissionSet.Id));
        access.RoleAssignments.Add(RoleAssignment.Grant(tenant, account.Id, role.Id, account.Id, RoleAssignment.SourceBootstrap));
        if (!await access.TenantAccessStates.AnyAsync(s => s.TenantId == tenant))
            access.TenantAccessStates.Add(TenantAccessState.Initialize(tenant));
        await access.SaveChangesAsync();
    }

    /// <summary>`ActionRegistryEntry` is a global catalog row; other tests may already have inserted it.</summary>
    private static async Task EnsureActionsRegisteredAsync(AccessDbContext access, params (string Key, string ResourceType)[] actions)
    {
        foreach (var (key, resourceType) in actions)
        {
            if (!await access.Actions.AnyAsync(a => a.ActionKey == key))
            {
                access.Actions.Add(ActionRegistryEntry.Create(key, "CRM", resourceType));
                await access.SaveChangesAsync();
            }
        }
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

    private sealed class ThrowingAuthorizer : IAuthorizer
    {
        public Task<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("pdp down");
    }

    /// <summary>Lets the party gate through and defers every other decision to the real PDP.</summary>
    private sealed class OpportunityReadOnlyAllowedAuthorizer(IAuthorizer real) : IAuthorizer
    {
        public Task<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken = default) =>
            request.Action.Value == PartySearch ? StubAuthorizer.AlwaysAllow.AuthorizeAsync(request, cancellationToken) : real.AuthorizeAsync(request, cancellationToken);
    }

    private sealed class ThrowingPartyDirectory : IPartyDirectory
    {
        public Task<PartyDirectoryEntry?> GetPartyAsync(PartyRef partyRef, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("masterdata down");

        public Task<IReadOnlyDictionary<PartyRef, PartyDirectoryEntry>> GetPartiesAsync(
            IReadOnlyCollection<PartyRef> partyRefs, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("masterdata down");
    }

    private sealed class RecordingPartyDirectory(IPartyDirectory inner) : IPartyDirectory
    {
        public List<long[]> Requested { get; } = [];

        public Task<PartyDirectoryEntry?> GetPartyAsync(PartyRef partyRef, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<PartyRef, PartyDirectoryEntry>> GetPartiesAsync(
            IReadOnlyCollection<PartyRef> partyRefs, CancellationToken cancellationToken = default)
        {
            Requested.Add([.. partyRefs.Select(r => r.PartyId).Order()]);
            return inner.GetPartiesAsync(partyRefs, cancellationToken);
        }
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Count { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.FromResult(result);
        }
    }

    /// <summary>Everything wired against the runtime role, exactly as Host composes it.</summary>
    private sealed class Harness : IAsyncDisposable
    {
        public required CrmDbContext Crm { get; init; }
        public required AccessDbContext Access { get; init; }
        public required MasterDataDbContext MasterData { get; init; }
        public required AccessAuthorizer Authorizer { get; init; }
        public required OpportunityLinkTargetResolver OpportunityResolver { get; init; }
        public required PartyLinkTargetResolver PartyResolver { get; init; }
        public required LinkTargetDirectory Directory { get; init; }

        public static async Task<Harness> CreateAsync(
            PostgresFixture fixture, DbCommandInterceptor? crmInterceptor = null, DbCommandInterceptor? accessInterceptor = null)
        {
            var connectionString = await fixture.RuntimeConnectionStringAsync();

            var crmOptions = new DbContextOptionsBuilder<CrmDbContext>()
                .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CrmDbContext.Schema))
                .UseSnakeCaseNamingConvention();
            if (crmInterceptor is not null)
                crmOptions.AddInterceptors(crmInterceptor);

            var crm = new CrmDbContext(crmOptions.Options);
            var accessOptions = new DbContextOptionsBuilder<AccessDbContext>()
                .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", AccessDbContext.AccessSchema))
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(new RowVersionInterceptor());
            if (accessInterceptor is not null)
                accessOptions.AddInterceptors(accessInterceptor);
            var access = new AccessDbContext(accessOptions.Options);
            var masterData = PostgresFixture.CreateMasterDataContext(connectionString);

            var authorizer = new AccessAuthorizer(access, new PrincipalResolver(access), new AccessActionCatalogService(access));
            var partyDirectory = new PartyDirectory(masterData);
            var opportunityResolver = new OpportunityLinkTargetResolver(crm, authorizer, partyDirectory);
            var partyResolver = new PartyLinkTargetResolver(authorizer, partyDirectory);

            return new Harness
            {
                Crm = crm,
                Access = access,
                MasterData = masterData,
                Authorizer = authorizer,
                OpportunityResolver = opportunityResolver,
                PartyResolver = partyResolver,
                Directory = new LinkTargetDirectory([opportunityResolver, partyResolver])
            };
        }

        public async ValueTask DisposeAsync()
        {
            await Crm.DisposeAsync();
            await Access.DisposeAsync();
            await MasterData.DisposeAsync();
        }
    }
}
