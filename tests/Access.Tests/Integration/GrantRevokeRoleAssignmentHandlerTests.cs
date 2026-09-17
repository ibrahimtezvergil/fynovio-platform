using Access.Application;
using Access.Domain.Identity;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Tests.Integration;

/// <summary>End-to-end proof of the round 3 §9 final invariant: authorize runs BEFORE
/// the idempotency lookup, so a principal whose grant authority is revoked between
/// two requests is denied on the next one rather than served a stale response.</summary>
public sealed class GrantRevokeRoleAssignmentHandlerTests : IClassFixture<PostgresFixture>
{
    private const string TenantAdministratorRoleKey = "tenant_administrator";
    private readonly PostgresFixture _fixture;

    public GrantRevokeRoleAssignmentHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    private static async Task<PrincipalRef> SeedActiveMemberAsync(Access.Persistence.AccessDbContext context, TenantId tenantId, string label)
    {
        var principal = new PrincipalRef("test-idp", $"{label}-{Guid.NewGuid():N}");
        var account = Account.Create($"{principal.Subject}@test.local", principal.Subject);
        context.Accounts.Add(account);
        await context.SaveChangesAsync();

        context.ExternalIdentities.Add(ExternalIdentity.Link(account.Id, principal));
        var membership = TenantMembership.Invite(tenantId, account.Id);
        membership.Activate();
        context.TenantMemberships.Add(membership);
        await context.SaveChangesAsync();

        return principal;
    }

    [Fact]
    public async Task Grant_replay_revoke_then_denied_regrant_by_the_now_unauthorized_principal()
    {
        var tenantId = new TenantId(600);
        PrincipalRef admin;
        PrincipalRef grantee;
        PrincipalRef thirdAccount;

        await using (var seed = _fixture.CreateAdminContext())
        {
            await AccessActionCatalogSeeder.EnsureSeededAsync(seed);
            admin = await SeedActiveMemberAsync(seed, tenantId, "admin");
            grantee = await SeedActiveMemberAsync(seed, tenantId, "grantee");
            thirdAccount = await SeedActiveMemberAsync(seed, tenantId, "third");
        }

        long adminAssignmentId;
        await using (var bootstrapContext = _fixture.CreateAdminContext())
        {
            var handler = new BootstrapTenantAccessHandler(bootstrapContext);
            await handler.HandleAsync(new BootstrapTenantAccessCommand(tenantId, admin, Guid.NewGuid()));
        }
        await using (var verify = _fixture.CreateAdminContext())
            adminAssignmentId = await verify.RoleAssignments.Where(a => a.TenantId == tenantId).Select(a => a.Id).SingleAsync();

        // 1. Grant tenant_administrator to `grantee`.
        var idempotencyKey = Guid.NewGuid().ToString("N");
        long granteeAssignmentId;
        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new GrantRoleAssignmentHandler(context, BuildAuthorizer(context));
            var result = await handler.HandleAsync(new GrantRoleAssignmentCommand(
                tenantId, admin, grantee, TenantAdministratorRoleKey, Reason: "test grant", Guid.NewGuid(), idempotencyKey));

            Assert.False(result.Replayed);
            granteeAssignmentId = result.RoleAssignmentId;
        }

        // 2. Replay with the SAME idempotency key and the same request -> Replayed: true, no new row.
        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new GrantRoleAssignmentHandler(context, BuildAuthorizer(context));
            var replay = await handler.HandleAsync(new GrantRoleAssignmentCommand(
                tenantId, admin, grantee, TenantAdministratorRoleKey, Reason: "test grant", Guid.NewGuid(), idempotencyKey));

            Assert.True(replay.Replayed);
            Assert.Equal(granteeAssignmentId, replay.RoleAssignmentId);
        }

        await using (var verify = _fixture.CreateAdminContext())
            Assert.Equal(1, await verify.RoleAssignments.CountAsync(a => a.TenantId == tenantId && a.Id == granteeAssignmentId));

        // 3. Same idempotency key, a DIFFERENT request -> IdempotencyKeyReusedException.
        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new GrantRoleAssignmentHandler(context, BuildAuthorizer(context));
            await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() => handler.HandleAsync(new GrantRoleAssignmentCommand(
                tenantId, admin, thirdAccount, TenantAdministratorRoleKey, Reason: "different grant", Guid.NewGuid(), idempotencyKey)));
        }

        // 4. Revoke the admin's OWN assignment.
        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new RevokeRoleAssignmentHandler(context, BuildAuthorizer(context));
            var result = await handler.HandleAsync(new RevokeRoleAssignmentCommand(
                tenantId, admin, adminAssignmentId, Reason: "test revoke", Guid.NewGuid(), Guid.NewGuid().ToString("N")));

            Assert.False(result.Replayed);
        }

        // 5. The now-unauthorized admin tries to grant again -> AuthorizationDeniedException.
        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new GrantRoleAssignmentHandler(context, BuildAuthorizer(context));
            var denied = await Assert.ThrowsAsync<AuthorizationDeniedException>(() => handler.HandleAsync(new GrantRoleAssignmentCommand(
                tenantId, admin, thirdAccount, TenantAdministratorRoleKey, Reason: null, Guid.NewGuid(), Guid.NewGuid().ToString("N"))));
            Assert.Contains("no_matching_grant", denied.Message);
        }
    }

    private static AccessAuthorizer BuildAuthorizer(Access.Persistence.AccessDbContext context) =>
        new(context, new PrincipalResolver(context), new AccessActionCatalogService(context));

    /// <summary>Bootstraps a fresh tenant and returns its admin/grantee principals plus
    /// the admin's own RoleAssignment id — the setup every test below starts from.</summary>
    private async Task<(TenantId TenantId, PrincipalRef Admin, PrincipalRef Grantee, long AdminAssignmentId)> GivenBootstrappedTenantAsync()
    {
        var tenantId = new TenantId(Random.Shared.NextInt64(1000, long.MaxValue));
        PrincipalRef admin;
        PrincipalRef grantee;

        await using (var seed = _fixture.CreateAdminContext())
        {
            await AccessActionCatalogSeeder.EnsureSeededAsync(seed);
            admin = await SeedActiveMemberAsync(seed, tenantId, "admin");
            grantee = await SeedActiveMemberAsync(seed, tenantId, "grantee");
        }

        await using (var bootstrap = _fixture.CreateAdminContext())
            await new BootstrapTenantAccessHandler(bootstrap).HandleAsync(new BootstrapTenantAccessCommand(tenantId, admin, Guid.NewGuid()));

        long adminAssignmentId;
        await using (var verify = _fixture.CreateAdminContext())
            adminAssignmentId = await verify.RoleAssignments.Where(a => a.TenantId == tenantId).Select(a => a.Id).SingleAsync();

        return (tenantId, admin, grantee, adminAssignmentId);
    }

    private async Task<long> GetRevisionAsync(TenantId tenantId)
    {
        await using var context = _fixture.CreateAdminContext();
        return await context.TenantAccessStates.Where(s => s.TenantId == tenantId).Select(s => s.Revision).SingleAsync();
    }

    /// <summary>C7a: a successful new grant must move the tenant's authorization
    /// epoch forward by exactly one — not zero (forgotten bump), not more than one
    /// (double bump on a single SaveChanges cycle).</summary>
    [Fact]
    public async Task Grant_increments_tenant_access_revision_exactly_once()
    {
        var (tenantId, admin, grantee, _) = await GivenBootstrappedTenantAsync();
        var revisionBefore = await GetRevisionAsync(tenantId);

        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new GrantRoleAssignmentHandler(context, BuildAuthorizer(context));
            await handler.HandleAsync(new GrantRoleAssignmentCommand(
                tenantId, admin, grantee, TenantAdministratorRoleKey, Reason: null, Guid.NewGuid(), Guid.NewGuid().ToString("N")));
        }

        Assert.Equal(revisionBefore + 1, await GetRevisionAsync(tenantId));
    }

    /// <summary>C7b: same invariant for revoke.</summary>
    [Fact]
    public async Task Revoke_increments_tenant_access_revision_exactly_once()
    {
        var (tenantId, admin, grantee, _) = await GivenBootstrappedTenantAsync();

        long granteeAssignmentId;
        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new GrantRoleAssignmentHandler(context, BuildAuthorizer(context));
            var result = await handler.HandleAsync(new GrantRoleAssignmentCommand(
                tenantId, admin, grantee, TenantAdministratorRoleKey, Reason: null, Guid.NewGuid(), Guid.NewGuid().ToString("N")));
            granteeAssignmentId = result.RoleAssignmentId;
        }

        var revisionBeforeRevoke = await GetRevisionAsync(tenantId);

        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new RevokeRoleAssignmentHandler(context, BuildAuthorizer(context));
            await handler.HandleAsync(new RevokeRoleAssignmentCommand(
                tenantId, admin, granteeAssignmentId, Reason: null, Guid.NewGuid(), Guid.NewGuid().ToString("N")));
        }

        Assert.Equal(revisionBeforeRevoke + 1, await GetRevisionAsync(tenantId));
    }

    /// <summary>C8a: replaying an already-applied grant with the same idempotency key
    /// returns the cached response without mutating state a second time — the
    /// revision must not move.</summary>
    [Fact]
    public async Task Replay_of_grant_does_not_increment_tenant_access_revision()
    {
        var (tenantId, admin, grantee, _) = await GivenBootstrappedTenantAsync();
        var idempotencyKey = Guid.NewGuid().ToString("N");

        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new GrantRoleAssignmentHandler(context, BuildAuthorizer(context));
            await handler.HandleAsync(new GrantRoleAssignmentCommand(
                tenantId, admin, grantee, TenantAdministratorRoleKey, Reason: null, Guid.NewGuid(), idempotencyKey));
        }

        var revisionAfterGrant = await GetRevisionAsync(tenantId);

        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new GrantRoleAssignmentHandler(context, BuildAuthorizer(context));
            var replay = await handler.HandleAsync(new GrantRoleAssignmentCommand(
                tenantId, admin, grantee, TenantAdministratorRoleKey, Reason: null, Guid.NewGuid(), idempotencyKey));
            Assert.True(replay.Replayed);
        }

        Assert.Equal(revisionAfterGrant, await GetRevisionAsync(tenantId));
    }

    /// <summary>C8b: a request an unauthorized principal never has a valid grant for
    /// must fail before ever touching TenantAccessState — the revision stays put.</summary>
    [Fact]
    public async Task Denied_grant_does_not_increment_tenant_access_revision()
    {
        var (tenantId, _, grantee, _) = await GivenBootstrappedTenantAsync();
        PrincipalRef unauthorized;
        await using (var seed = _fixture.CreateAdminContext())
            unauthorized = await SeedActiveMemberAsync(seed, tenantId, "unauthorized");
        var revisionBefore = await GetRevisionAsync(tenantId);

        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new GrantRoleAssignmentHandler(context, BuildAuthorizer(context));
            await Assert.ThrowsAsync<AuthorizationDeniedException>(() => handler.HandleAsync(new GrantRoleAssignmentCommand(
                tenantId, unauthorized, grantee, TenantAdministratorRoleKey, Reason: null, Guid.NewGuid(), Guid.NewGuid().ToString("N"))));
        }

        Assert.Equal(revisionBefore, await GetRevisionAsync(tenantId));
    }

    /// <summary>C9: the round 3 §9 invariant end to end for a REPLAY, not just a new
    /// request. Authorize runs before the idempotency lookup, so replaying the exact
    /// same successful request+key after the granter's own authority is revoked must
    /// fail closed rather than return the old cached success.</summary>
    [Fact]
    public async Task Previously_successful_request_cannot_replay_after_authority_revocation()
    {
        var (tenantId, admin, grantee, adminAssignmentId) = await GivenBootstrappedTenantAsync();
        var idempotencyKey = Guid.NewGuid().ToString("N");

        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new GrantRoleAssignmentHandler(context, BuildAuthorizer(context));
            var result = await handler.HandleAsync(new GrantRoleAssignmentCommand(
                tenantId, admin, grantee, TenantAdministratorRoleKey, Reason: null, Guid.NewGuid(), idempotencyKey));
            Assert.False(result.Replayed);
        }

        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new RevokeRoleAssignmentHandler(context, BuildAuthorizer(context));
            await handler.HandleAsync(new RevokeRoleAssignmentCommand(
                tenantId, admin, adminAssignmentId, Reason: "authority removed", Guid.NewGuid(), Guid.NewGuid().ToString("N")));
        }

        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new GrantRoleAssignmentHandler(context, BuildAuthorizer(context));
            var denied = await Assert.ThrowsAsync<AuthorizationDeniedException>(() => handler.HandleAsync(new GrantRoleAssignmentCommand(
                tenantId, admin, grantee, TenantAdministratorRoleKey, Reason: null, Guid.NewGuid(), idempotencyKey)));
            Assert.Contains("no_matching_grant", denied.Message);
        }
    }

    /// <summary>C10a: exactly one evidence record and one outbox message per
    /// successful grant — not zero, not duplicated by the two-SaveChanges shape of
    /// the handler.</summary>
    [Fact]
    public async Task Grant_emits_exactly_one_evidence_and_outbox_record()
    {
        var (tenantId, admin, grantee, _) = await GivenBootstrappedTenantAsync();
        long granteeAssignmentId;

        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new GrantRoleAssignmentHandler(context, BuildAuthorizer(context));
            var result = await handler.HandleAsync(new GrantRoleAssignmentCommand(
                tenantId, admin, grantee, TenantAdministratorRoleKey, Reason: null, Guid.NewGuid(), Guid.NewGuid().ToString("N")));
            granteeAssignmentId = result.RoleAssignmentId;
        }

        await using var verify = _fixture.CreateAdminContext();
        Assert.Equal(1, await verify.EvidenceRecords.CountAsync(
            e => e.TenantId == tenantId && e.AggregateId == granteeAssignmentId && e.Action == "RoleAssignment.Grant"));
        Assert.Equal(1, await verify.OutboxMessages.CountAsync(
            m => m.TenantId == tenantId && m.AggregateId == granteeAssignmentId && m.EventType == "enterprise.access.role_assignment.granted.v1"));
    }

    /// <summary>C10b: same invariant for revoke.</summary>
    [Fact]
    public async Task Revoke_emits_exactly_one_evidence_and_outbox_record()
    {
        var (tenantId, admin, grantee, _) = await GivenBootstrappedTenantAsync();
        long granteeAssignmentId;

        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new GrantRoleAssignmentHandler(context, BuildAuthorizer(context));
            var result = await handler.HandleAsync(new GrantRoleAssignmentCommand(
                tenantId, admin, grantee, TenantAdministratorRoleKey, Reason: null, Guid.NewGuid(), Guid.NewGuid().ToString("N")));
            granteeAssignmentId = result.RoleAssignmentId;
        }

        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new RevokeRoleAssignmentHandler(context, BuildAuthorizer(context));
            await handler.HandleAsync(new RevokeRoleAssignmentCommand(
                tenantId, admin, granteeAssignmentId, Reason: null, Guid.NewGuid(), Guid.NewGuid().ToString("N")));
        }

        await using var verify = _fixture.CreateAdminContext();
        Assert.Equal(1, await verify.EvidenceRecords.CountAsync(
            e => e.TenantId == tenantId && e.AggregateId == granteeAssignmentId && e.Action == "RoleAssignment.Revoke"));
        Assert.Equal(1, await verify.OutboxMessages.CountAsync(
            m => m.TenantId == tenantId && m.AggregateId == granteeAssignmentId && m.EventType == "enterprise.access.role_assignment.revoked.v1"));
    }

    /// <summary>C10c: a replay must not write a second evidence/outbox pair — the
    /// early-return-on-cache-hit path skips the mutation section entirely.</summary>
    [Fact]
    public async Task Replay_does_not_emit_additional_evidence_or_outbox_records()
    {
        var (tenantId, admin, grantee, _) = await GivenBootstrappedTenantAsync();
        var idempotencyKey = Guid.NewGuid().ToString("N");
        long granteeAssignmentId;

        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new GrantRoleAssignmentHandler(context, BuildAuthorizer(context));
            var result = await handler.HandleAsync(new GrantRoleAssignmentCommand(
                tenantId, admin, grantee, TenantAdministratorRoleKey, Reason: null, Guid.NewGuid(), idempotencyKey));
            granteeAssignmentId = result.RoleAssignmentId;
        }

        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new GrantRoleAssignmentHandler(context, BuildAuthorizer(context));
            var replay = await handler.HandleAsync(new GrantRoleAssignmentCommand(
                tenantId, admin, grantee, TenantAdministratorRoleKey, Reason: null, Guid.NewGuid(), idempotencyKey));
            Assert.True(replay.Replayed);
        }

        await using var verify = _fixture.CreateAdminContext();
        Assert.Equal(1, await verify.EvidenceRecords.CountAsync(
            e => e.TenantId == tenantId && e.AggregateId == granteeAssignmentId && e.Action == "RoleAssignment.Grant"));
        Assert.Equal(1, await verify.OutboxMessages.CountAsync(
            m => m.TenantId == tenantId && m.AggregateId == granteeAssignmentId && m.EventType == "enterprise.access.role_assignment.granted.v1"));
    }

    /// <summary>H9: the idempotency lookup is scoped to tenant + principal + operation
    /// + request hash (GrantRoleAssignmentHandler.cs's IdempotencyRecords filter) — not
    /// the raw key string alone. The same key string used by two different tenants'
    /// admins must not collide.</summary>
    [Fact]
    public async Task Idempotency_key_reuse_across_different_tenants_does_not_collide()
    {
        var (tenantOne, adminOne, granteeOne, _) = await GivenBootstrappedTenantAsync();
        var (tenantTwo, adminTwo, granteeTwo, _) = await GivenBootstrappedTenantAsync();
        var sharedIdempotencyKey = Guid.NewGuid().ToString("N");

        long firstAssignmentId;
        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new GrantRoleAssignmentHandler(context, BuildAuthorizer(context));
            var result = await handler.HandleAsync(new GrantRoleAssignmentCommand(
                tenantOne, adminOne, granteeOne, TenantAdministratorRoleKey, Reason: null, Guid.NewGuid(), sharedIdempotencyKey));
            Assert.False(result.Replayed);
            firstAssignmentId = result.RoleAssignmentId;
        }

        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new GrantRoleAssignmentHandler(context, BuildAuthorizer(context));
            var result = await handler.HandleAsync(new GrantRoleAssignmentCommand(
                tenantTwo, adminTwo, granteeTwo, TenantAdministratorRoleKey, Reason: null, Guid.NewGuid(), sharedIdempotencyKey));
            Assert.False(result.Replayed);
            Assert.NotEqual(firstAssignmentId, result.RoleAssignmentId);
        }
    }
}
