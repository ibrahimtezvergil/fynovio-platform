using Access.Application;
using Access.Domain.Authorization;
using Access.Domain.Identity;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Access.Tests.Integration;

/// <summary>2026-09-20 PHASE_1_5_RUNTIME_RLS_PDP_DELTA: <see cref="AccessRlsTests"/> proves
/// RLS itself works at the raw-SQL level under the unprivileged runtime role; these tests
/// prove the actual PDP classes (`PrincipalResolver`, `AccessAuthorizer`,
/// `AccessScopeResolver`) establish their own tenant context correctly when run as that same
/// role — the gap that let the whole authorization system fail-closed in production despite
/// every prior test suite passing (they all ran these classes against the superuser
/// connection, which bypasses RLS unconditionally).</summary>
public sealed class PdpRuntimeRoleTests : IClassFixture<PostgresFixture>
{
    private const string ActionKey = "pdp.runtime_test.action";
    private readonly PostgresFixture _fixture;

    public PdpRuntimeRoleTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Membership_resolves_under_the_members_own_tenant_but_not_under_a_foreign_tenant()
    {
        var tenantA = TestData.NextTenant();
        var tenantB = TestData.NextTenant();
        var principal = FreshPrincipal();

        await using (var admin = _fixture.CreateAdminContext())
        {
            var account = Account.Create($"{principal.Subject}@test.local", principal.Subject);
            admin.Accounts.Add(account);
            await admin.SaveChangesAsync();
            admin.ExternalIdentities.Add(ExternalIdentity.Link(account.Id, principal));
            var membership = TenantMembership.Invite(tenantA, account.Id);
            membership.Activate();
            admin.TenantMemberships.Add(membership);
            await admin.SaveChangesAsync();
        }

        var resolver = new PrincipalResolver(await CreateRuntimeContextAsync());

        Assert.True(await resolver.IsActiveTenantMemberAsync(principal, tenantA));
        Assert.False(await resolver.IsActiveTenantMemberAsync(principal, tenantB));
    }

    [Fact]
    public async Task Tenant_wide_grant_authorizes_under_its_own_tenant_and_is_invisible_under_another_tenant()
    {
        var tenantA = TestData.NextTenant();
        var tenantB = TestData.NextTenant();
        var principal = FreshPrincipal();

        await using (var admin = _fixture.CreateAdminContext())
        {
            await SeedRegisteredActionAsync(admin);
            var account = Account.Create($"{principal.Subject}@test.local", principal.Subject);
            admin.Accounts.Add(account);
            await admin.SaveChangesAsync();
            admin.ExternalIdentities.Add(ExternalIdentity.Link(account.Id, principal));
            admin.TenantAccessStates.Add(TenantAccessState.Initialize(tenantA));
            admin.TenantAccessStates.Add(TenantAccessState.Initialize(tenantB));
            await GrantAsync(admin, tenantA, account.Id, relation: null);
            await admin.SaveChangesAsync();
        }

        var runtimeContext = await CreateRuntimeContextAsync();
        var authorizer = new AccessAuthorizer(runtimeContext, new PrincipalResolver(runtimeContext), new AccessActionCatalogService(runtimeContext));

        var allowed = await authorizer.AuthorizeAsync(new AuthorizationRequest(
            new ActorContext(tenantA, principal, Guid.NewGuid()), new ActionKey(ActionKey), new ResourceDescriptor("Test", null, null)));
        Assert.Equal(AuthorizationEffect.Allow, allowed.Effect);

        var denied = await authorizer.AuthorizeAsync(new AuthorizationRequest(
            new ActorContext(tenantB, principal, Guid.NewGuid()), new ActionKey(ActionKey), new ResourceDescriptor("Test", null, null)));
        Assert.Equal(AuthorizationEffect.Deny, denied.Effect);
        Assert.Equal(AuthorizationDenialStage.Coarse, denied.DenialStage);
    }

    [Fact]
    public async Task Owner_relation_grant_authorizes_only_the_owned_resource_under_the_runtime_role()
    {
        var tenant = TestData.NextTenant();
        var owner = FreshPrincipal();
        var stranger = FreshPrincipal();

        await using (var admin = _fixture.CreateAdminContext())
        {
            await SeedRegisteredActionAsync(admin);
            var ownerAccount = Account.Create($"{owner.Subject}@test.local", owner.Subject);
            var strangerAccount = Account.Create($"{stranger.Subject}@test.local", stranger.Subject);
            admin.Accounts.AddRange(ownerAccount, strangerAccount);
            await admin.SaveChangesAsync();
            admin.ExternalIdentities.Add(ExternalIdentity.Link(ownerAccount.Id, owner));
            admin.ExternalIdentities.Add(ExternalIdentity.Link(strangerAccount.Id, stranger));
            admin.TenantAccessStates.Add(TenantAccessState.Initialize(tenant));
            await GrantAsync(admin, tenant, ownerAccount.Id, relation: PermissionSetItem.OwnerRelation);
            await GrantAsync(admin, tenant, strangerAccount.Id, relation: PermissionSetItem.OwnerRelation);
            await admin.SaveChangesAsync();
        }

        var runtimeContext = await CreateRuntimeContextAsync();
        var authorizer = new AccessAuthorizer(runtimeContext, new PrincipalResolver(runtimeContext), new AccessActionCatalogService(runtimeContext));
        var resource = new ResourceDescriptor("Test", 1, owner);

        var ownerDecision = await authorizer.AuthorizeAsync(new AuthorizationRequest(
            new ActorContext(tenant, owner, Guid.NewGuid()), new ActionKey(ActionKey), resource));
        Assert.Equal(AuthorizationEffect.Allow, ownerDecision.Effect);

        var strangerDecision = await authorizer.AuthorizeAsync(new AuthorizationRequest(
            new ActorContext(tenant, stranger, Guid.NewGuid()), new ActionKey(ActionKey), resource));
        Assert.Equal(AuthorizationEffect.Deny, strangerDecision.Effect);
        Assert.Equal(AuthorizationDenialStage.Record, strangerDecision.DenialStage);
    }

    /// <summary>Pooled-connection non-leak: the same `AccessDbContext` instance (one
    /// physical connection under test) evaluates Tenant A then Tenant B sequentially —
    /// each call must open and commit its own transaction-local `app.tenant_id`, never
    /// carrying Tenant A's visibility into Tenant B's evaluation.</summary>
    [Fact]
    public async Task Sequential_authorization_calls_on_the_same_connection_do_not_leak_tenant_context()
    {
        var tenantA = TestData.NextTenant();
        var tenantB = TestData.NextTenant();
        var principal = FreshPrincipal();

        await using (var admin = _fixture.CreateAdminContext())
        {
            await SeedRegisteredActionAsync(admin);
            var account = Account.Create($"{principal.Subject}@test.local", principal.Subject);
            admin.Accounts.Add(account);
            await admin.SaveChangesAsync();
            admin.ExternalIdentities.Add(ExternalIdentity.Link(account.Id, principal));
            admin.TenantAccessStates.Add(TenantAccessState.Initialize(tenantA));
            admin.TenantAccessStates.Add(TenantAccessState.Initialize(tenantB));
            // Grant only in Tenant A — Tenant B has zero grants for this principal/action.
            await GrantAsync(admin, tenantA, account.Id, relation: null);
            await admin.SaveChangesAsync();
        }

        var runtimeContext = await CreateRuntimeContextAsync();
        var authorizer = new AccessAuthorizer(runtimeContext, new PrincipalResolver(runtimeContext), new AccessActionCatalogService(runtimeContext));

        var first = await authorizer.AuthorizeAsync(new AuthorizationRequest(
            new ActorContext(tenantA, principal, Guid.NewGuid()), new ActionKey(ActionKey), new ResourceDescriptor("Test", null, null)));
        var second = await authorizer.AuthorizeAsync(new AuthorizationRequest(
            new ActorContext(tenantB, principal, Guid.NewGuid()), new ActionKey(ActionKey), new ResourceDescriptor("Test", null, null)));

        Assert.Equal(AuthorizationEffect.Allow, first.Effect);
        Assert.Equal(AuthorizationEffect.Deny, second.Effect);
        Assert.Equal(AuthorizationDenialStage.Coarse, second.DenialStage);
    }

    private async Task<Access.Persistence.AccessDbContext> CreateRuntimeContextAsync() =>
        PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());

    private static PrincipalRef FreshPrincipal() => new("https://idp.local", $"pdp-runtime-test-{Guid.NewGuid():N}");

    private static async Task SeedRegisteredActionAsync(Access.Persistence.AccessDbContext admin)
    {
        if (!await admin.Actions.AnyAsync(a => a.ActionKey == ActionKey))
        {
            admin.Actions.Add(ActionRegistryEntry.Create(ActionKey, "Test", "Test"));
            await admin.SaveChangesAsync();
        }
    }

    private static async Task GrantAsync(Access.Persistence.AccessDbContext admin, TenantId tenant, long accountId, string? relation)
    {
        var suffix = "t" + Guid.NewGuid().ToString("N")[..7];
        var permissionSet = PermissionSet.Create(tenant, $"pdp_test_permission_set_{suffix}", "PDP Test Permission Set");
        permissionSet.Grant(ActionKey, relation);
        admin.PermissionSets.Add(permissionSet);

        var role = Role.Create(tenant, $"pdp_test_role_{suffix}", $"PDP Test Role {suffix}");
        admin.Roles.Add(role);
        await admin.SaveChangesAsync();

        admin.RolePermissionSets.Add(RolePermissionSet.Create(tenant, role.Id, permissionSet.Id));
        admin.RoleAssignments.Add(RoleAssignment.Grant(tenant, accountId, role.Id, accountId, RoleAssignment.SourceBootstrap));
    }
}
