using Access.Application;
using Access.Domain.Authorization;
using Access.Domain.Identity;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Access.Tests.Application;

/// <summary>Shared setup for PDP/scope-resolver tests: a real Postgres-backed
/// `AccessDbContext` (LINQ joins over `HasConversion`'d `TenantId` need real SQL
/// translation, not the in-memory provider), a tenant, an account/external identity,
/// and a helper to seed a role + permission-set grant. Uses the admin (superuser)
/// connection — RLS itself is exercised separately in `AccessRlsTests`.</summary>
public sealed class AccessTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("fynovio_platform_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public AccessDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AccessDbContext>()
            .UseNpgsql(_container.GetConnectionString(), npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", AccessDbContext.AccessSchema))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AccessDbContext(options);
    }

    /// <summary>Seeds a tenant, an Account/ExternalIdentity for the given principal, a
    /// Role granting `actionKey` (optionally with `relation`), an active RoleAssignment,
    /// and a `TenantAccessState` row. Returns wired `AccessAuthorizer`/
    /// `AccessScopeResolver` instances plus the ActorContext to use in assertions.
    /// Role/PermissionSet keys and the action key get a per-call random suffix so
    /// repeated calls against the same shared test container (e.g. multiple `[Theory]`
    /// iterations) never collide on the `(tenant_id, key)`/action-key unique
    /// constraints, even when reusing the same `tenantId`.</summary>
    public async Task<(AccessAuthorizer Authorizer, AccessScopeResolver ScopeResolver, ActorContext Actor, ActionKey Action)> GivenGrantAsync(
        TenantId tenantId, PrincipalRef principal, string actionKey, string ownerModule, string resourceType, string? relation)
    {
        // "t" prefix guarantees the suffix always starts with a letter — a raw hex
        // GUID substring can start with a digit, which would violate ActionKey's own
        // "segment starts with a letter" format rule.
        var suffix = "t" + Guid.NewGuid().ToString("N")[..7];
        var uniqueActionKey = $"{actionKey}.{suffix}";

        await using var context = CreateContext();

        var account = Account.Create($"{principal.Subject}-{suffix}@test.local", principal.Subject);
        context.Accounts.Add(account);
        await context.SaveChangesAsync();

        context.ExternalIdentities.Add(ExternalIdentity.Link(account.Id, principal));
        context.TenantMemberships.Add(TenantMembership.Invite(tenantId, account.Id));

        context.Actions.Add(ActionRegistryEntry.Create(uniqueActionKey, ownerModule, resourceType));

        var permissionSet = PermissionSet.Create(tenantId, $"test_permission_set_{suffix}", "Test Permission Set");
        permissionSet.Grant(uniqueActionKey, relation);
        context.PermissionSets.Add(permissionSet);

        // Role has a unique index on BOTH (tenant_id, key) and (tenant_id, name) —
        // the name needs the same per-call suffix, not just the key.
        var role = Role.Create(tenantId, $"test_role_{suffix}", $"Test Role {suffix}");
        context.Roles.Add(role);
        await context.SaveChangesAsync();

        context.RolePermissionSets.Add(RolePermissionSet.Create(tenantId, role.Id, permissionSet.Id));
        context.RoleAssignments.Add(RoleAssignment.Grant(tenantId, account.Id, role.Id, account.Id, RoleAssignment.SourceBootstrap));
        context.TenantAccessStates.Add(TenantAccessState.Initialize(tenantId));
        await context.SaveChangesAsync();

        var authorizerContext = CreateContext();
        var scopeResolverContext = CreateContext();
        var actionCatalog = new AccessActionCatalogService(authorizerContext);
        var authorizer = new AccessAuthorizer(authorizerContext, new PrincipalResolver(authorizerContext), actionCatalog);
        var scopeResolver = new AccessScopeResolver(scopeResolverContext, new PrincipalResolver(scopeResolverContext), new AccessActionCatalogService(scopeResolverContext));

        return (authorizer, scopeResolver, new ActorContext(tenantId, principal, Guid.NewGuid()), new ActionKey(uniqueActionKey));
    }

    /// <summary>Uses a fresh, unique tenant and `PrincipalRef` on every call —
    /// `ExternalIdentity`'s `(issuer, subject)` unique index and
    /// `TenantAccessState`'s `tenant_id` PK would otherwise collide across repeated
    /// `[Theory]` iterations against the same shared container database.</summary>
    public async Task<(AccessAuthorizer Authorizer, AccessScopeResolver ScopeResolver, ActorContext Actor, ActionKey Action)> GivenOwnerRelationGrantAsync()
    {
        var tenantId = new TenantId(Random.Shared.NextInt64(1, long.MaxValue));
        var principal = new PrincipalRef("test-idp", $"owner-relation-actor-{Guid.NewGuid():N}");
        return await GivenGrantAsync(tenantId, principal, "test.resource.read", "Test", "Test.Resource", PermissionSetItem.OwnerRelation);
    }
}
