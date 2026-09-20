using Access.Application;
using Access.Application.Authentication;
using Access.Domain.Authorization;
using Access.Domain.Identity;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Tests.Integration;

/// <summary>The Assignable-Principals building block: which ACTIVE members of a tenant would be permitted every
/// required action as owner of a record. Its rule must agree with the PDP for every member.</summary>
public sealed class AuthorizedPrincipalDirectoryTests : IClassFixture<PostgresFixture>
{
    private const string Issuer = "fynovio-test-issuer";
    private const string Read = "demo.record.read";
    private const string Stage = "demo.record.stage";

    private static readonly ActionKey[] Required = [new(Read), new(Stage)];
    private static readonly SessionOptions Options = new() { PlatformIssuer = Issuer };

    private readonly PostgresFixture _fixture;

    public AuthorizedPrincipalDirectoryTests(PostgresFixture fixture) => _fixture = fixture;

    private sealed record Member(string Label, long AccountId, PrincipalRef Principal);

    private static AuthorizedPrincipalDirectory DirectoryFor(AccessDbContext context) =>
        new(context, new AccessActionCatalogService(context), Options);

    private async Task SeedRegistryAsync()
    {
        await using var context = _fixture.CreateAdminContext();
        await AccessActionCatalogSeeder.EnsureSeededAsync(context,
        [
            .. AccessActionCatalog.All,
            new ActionRegistryDescriptor(Read, "Demo", "Record"),
            new ActionRegistryDescriptor(Stage, "Demo", "Record")
        ]);
    }

    private static async Task<Member> AddMemberAsync(
        AccessDbContext context, TenantId tenant, string label, string issuer = Issuer, string status = "active", string? displayName = null, string? email = null)
    {
        var subject = $"{label}-{Guid.NewGuid():N}";
        var account = Account.Create(email ?? $"{subject}@test.local", displayName ?? label);
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        var principal = new PrincipalRef(issuer, subject);
        context.ExternalIdentities.Add(ExternalIdentity.Link(account.Id, principal));
        var membership = TenantMembership.Invite(tenant, account.Id);
        if (status is "active" or "disabled")
            membership.Activate();
        if (status == "disabled")
            membership.Disable();
        context.TenantMemberships.Add(membership);
        await context.SaveChangesAsync();
        return new Member(label, account.Id, principal);
    }

    private static async Task GrantAsync(
        AccessDbContext context, TenantId tenant, Member member, (string Action, string? Relation)[] items,
        DateTimeOffset? validFrom = null, DateTimeOffset? validTo = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var set = PermissionSet.Create(tenant, $"set_{suffix}", $"Set {suffix}");
        foreach (var (action, relation) in items)
            set.Grant(action, relation);
        context.PermissionSets.Add(set);
        var role = Role.Create(tenant, $"role_{suffix}", $"Role {suffix}");
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        context.RolePermissionSets.Add(RolePermissionSet.Create(tenant, role.Id, set.Id));
        var assignment = RoleAssignment.Grant(tenant, member.AccountId, role.Id, member.AccountId, RoleAssignment.SourceManual, null, validFrom);
        context.RoleAssignments.Add(assignment);
        await context.SaveChangesAsync();
        if (validTo is not null)
        {
            assignment.Revoke(validTo);
            await context.SaveChangesAsync();
        }
    }

    private async Task<(TenantId Tenant, Dictionary<string, Member> Members)> MatrixAsync()
    {
        await SeedRegistryAsync();
        var tenant = TestData.NextTenant();
        var other = TestData.NextTenant();
        var members = new Dictionary<string, Member>();
        await using var context = _fixture.CreateAdminContext();

        async Task<Member> Add(string label, string issuer = Issuer, string status = "active")
        {
            var member = await AddMemberAsync(context, tenant, label, issuer, status);
            members[label] = member;
            return member;
        }

        (string, string?)[] both = [(Read, null), (Stage, null)];
        (string, string?)[] bothAsOwner = [(Read, "owner"), (Stage, "owner")];

        await GrantAsync(context, tenant, await Add("manager"), both);
        await GrantAsync(context, tenant, await Add("owner_only"), bothAsOwner);
        var split = await Add("split");
        await GrantAsync(context, tenant, split, [(Read, null)]);
        await GrantAsync(context, tenant, split, [(Stage, "owner")]);
        await GrantAsync(context, tenant, await Add("viewer"), [(Read, null)]);
        await GrantAsync(context, tenant, await Add("stager"), [(Stage, null)]);
        await GrantAsync(context, tenant, await Add("expired"), both, validFrom: DateTimeOffset.UtcNow.AddDays(-10), validTo: DateTimeOffset.UtcNow.AddDays(-1));
        await GrantAsync(context, tenant, await Add("future"), both, validFrom: DateTimeOffset.UtcNow.AddDays(5));
        await GrantAsync(context, tenant, await Add("disabled", status: "disabled"), both);
        await GrantAsync(context, tenant, await Add("invited", status: "invited"), both);
        await Add("no_grants");
        await GrantAsync(context, tenant, await Add("foreign_issuer", issuer: "someone-else"), both);

        // Full grants, but in ANOTHER tenant — never a candidate here.
        var elsewhere = await AddMemberAsync(context, other, "elsewhere");
        await GrantAsync(context, other, elsewhere, both);
        members["elsewhere"] = elsewhere;

        return (tenant, members);
    }

    private static readonly string[] ExpectedPermitted = ["manager", "owner_only", "split"];

    [Fact]
    public async Task Lists_exactly_the_active_members_permitted_every_required_action()
    {
        var (tenant, members) = await MatrixAsync();
        await using var context = _fixture.CreateAdminContext();

        var listed = await DirectoryFor(context).ListPermittedPrincipalsAsync(tenant, Required, null, 50);

        Assert.Equal(ExpectedPermitted.Order(), listed.Select(e => e.DisplayName).Order());
        Assert.All(listed, e => Assert.Equal(members[e.DisplayName].Principal, e.Principal));
    }

    /// <summary>The invariant that keeps this list honest: for every member, "listed" == "the PDP allows every required
    /// action to that member as owner" (membership state and issuer being the directory's own extra gates).</summary>
    [Fact]
    public async Task Agrees_with_the_PDP_for_every_member_of_the_matrix()
    {
        var (tenant, members) = await MatrixAsync();
        await using var context = _fixture.CreateAdminContext();
        var directory = DirectoryFor(context);
        var authorizer = new AccessAuthorizer(context, new PrincipalResolver(context), new AccessActionCatalogService(context));

        foreach (var (label, member) in members.Where(m => m.Key != "elsewhere"))
        {
            var asOwner = new ResourceDescriptor("Record", 1, member.Principal);
            var actor = new ActorContext(tenant, member.Principal, Guid.NewGuid());
            var allowedByPdp = true;
            foreach (var action in Required)
                allowedByPdp &= (await authorizer.AuthorizeAsync(new AuthorizationRequest(actor, action, asOwner))).IsAllowed;

            var directoryGate = label is not ("disabled" or "invited" or "foreign_issuer");
            var expected = allowedByPdp && directoryGate;

            Assert.Equal(expected, await directory.IsPrincipalPermittedAsync(tenant, member.Principal, Required));
            Assert.Equal(expected, (await directory.ListPermittedPrincipalsAsync(tenant, Required, null, 50)).Any(e => e.Principal == member.Principal));
        }
    }

    [Fact]
    public async Task A_member_of_another_tenant_is_neither_listed_nor_permitted()
    {
        var (tenant, members) = await MatrixAsync();
        await using var context = _fixture.CreateAdminContext();
        var directory = DirectoryFor(context);

        Assert.False(await directory.IsPrincipalPermittedAsync(tenant, members["elsewhere"].Principal, Required));
        Assert.DoesNotContain(await directory.ListPermittedPrincipalsAsync(tenant, Required, null, 50), e => e.DisplayName == "elsewhere");
    }

    [Fact]
    public async Task An_unregistered_or_deprecated_action_yields_nobody()
    {
        var (tenant, members) = await MatrixAsync();
        await using var context = _fixture.CreateAdminContext();
        var directory = DirectoryFor(context);
        ActionKey[] unknown = [new(Read), new("demo.record.not_registered")];

        Assert.Empty(await directory.ListPermittedPrincipalsAsync(tenant, unknown, null, 50));
        Assert.False(await directory.IsPrincipalPermittedAsync(tenant, members["manager"].Principal, unknown));

        // Deprecating a required action fails closed as well.
        await AccessActionCatalogSeeder.EnsureSeededAsync(context, AccessActionCatalog.All.Concat([new ActionRegistryDescriptor(Read, "Demo", "Record")]));
        Assert.Empty(await DirectoryFor(context).ListPermittedPrincipalsAsync(tenant, Required, null, 50));
        await SeedRegistryAsync();
    }

    [Fact]
    public async Task An_empty_requirement_is_a_caller_bug_not_everybody()
    {
        var (tenant, _) = await MatrixAsync();
        await using var context = _fixture.CreateAdminContext();

        await Assert.ThrowsAsync<ArgumentException>(() => DirectoryFor(context).ListPermittedPrincipalsAsync(tenant, [], null, 10));
    }

    [Fact]
    public async Task Search_matches_name_or_email_case_insensitively_and_treats_wildcards_literally()
    {
        await SeedRegistryAsync();
        var tenant = TestData.NextTenant();
        await using var context = _fixture.CreateAdminContext();
        (string, string?)[] both = [(Read, null), (Stage, null)];
        foreach (var (name, email) in new[] { ("Ada Lovelace", "ada@analytical.test"), ("Grace Hopper", "grace@navy.test"), ("100%_Sure", "sure@literal.test") })
            await GrantAsync(context, tenant, await AddMemberAsync(context, tenant, "m", displayName: name, email: email), both);
        var directory = DirectoryFor(context);

        async Task<List<string>> Names(string? search) =>
            (await directory.ListPermittedPrincipalsAsync(tenant, Required, search, 50)).Select(e => e.DisplayName).ToList();

        Assert.Equal(["Ada Lovelace"], await Names("LOVE"));
        Assert.Equal(["Grace Hopper"], await Names("navy.test"));
        Assert.Equal(["100%_Sure"], await Names("%_"));       // literal, not "match everything"
        Assert.Empty(await Names("%zzz"));
        Assert.Equal(3, (await Names("   ")).Count);           // blank search = no filter
        Assert.Equal(["100%_Sure", "Ada Lovelace", "Grace Hopper"], await Names(null)); // ordered by display name
    }

    [Fact]
    public async Task Take_is_clamped_to_the_documented_maximum_and_at_least_one()
    {
        await SeedRegistryAsync();
        var tenant = TestData.NextTenant();
        await using var context = _fixture.CreateAdminContext();
        (string, string?)[] both = [(Read, null), (Stage, null)];
        for (var i = 0; i < AuthorizedPrincipalDirectory.MaxTake + 5; i++)
            await GrantAsync(context, tenant, await AddMemberAsync(context, tenant, $"bulk{i:D3}"), both);
        var directory = DirectoryFor(context);

        Assert.Equal(AuthorizedPrincipalDirectory.MaxTake, (await directory.ListPermittedPrincipalsAsync(tenant, Required, null, 10_000)).Count);
        Assert.Single(await directory.ListPermittedPrincipalsAsync(tenant, Required, null, 0));
        Assert.Equal(3, (await directory.ListPermittedPrincipalsAsync(tenant, Required, null, 3)).Count);
    }

    [Fact]
    public async Task Works_as_the_unprivileged_runtime_role_under_row_level_security()
    {
        var (tenant, _) = await MatrixAsync();
        var runtime = await _fixture.RuntimeConnectionStringAsync();
        await using var context = PostgresFixture.CreateContext(runtime);

        var listed = await DirectoryFor(context).ListPermittedPrincipalsAsync(tenant, Required, null, 50);

        Assert.Equal(ExpectedPermitted.Order(), listed.Select(e => e.DisplayName).Order());
    }

    [Fact]
    public async Task A_missing_platform_issuer_is_a_configuration_error()
    {
        var (tenant, _) = await MatrixAsync();
        await using var context = _fixture.CreateAdminContext();
        var directory = new AuthorizedPrincipalDirectory(context, new AccessActionCatalogService(context), new SessionOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(() => directory.ListPermittedPrincipalsAsync(tenant, Required, null, 5));
    }
}
