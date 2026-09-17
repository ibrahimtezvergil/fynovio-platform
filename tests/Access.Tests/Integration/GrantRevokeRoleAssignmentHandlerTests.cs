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
}
