using Access.Application;
using Access.Domain.Authorization;
using Access.Domain.Identity;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Tests.Integration;

public sealed class EnsureTenantAdministratorActionsHandlerTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly string[] CrmSettingsActions = ["crm.settings.read", "crm.settings.update"];

    [Fact]
    public async Task Adds_only_the_requested_capabilities_to_an_existing_admin_and_is_idempotent()
    {
        var tenant = TestData.NextTenant();
        var adminPrincipal = new PrincipalRef("test-idp", $"admin-{Guid.NewGuid():N}");
        var ordinaryPrincipal = new PrincipalRef("test-idp", $"member-{Guid.NewGuid():N}");
        await using (var seed = fixture.CreateAdminContext())
        {
            await AccessActionCatalogSeeder.EnsureSeededAsync(seed, AccessActionCatalog.All);
            foreach (var principal in new[] { adminPrincipal, ordinaryPrincipal })
            {
                var account = Account.Create($"{principal.Subject}@test.local", principal.Subject);
                seed.Accounts.Add(account);
                await seed.SaveChangesAsync();
                seed.ExternalIdentities.Add(ExternalIdentity.Link(account.Id, principal));
                var membership = TenantMembership.Invite(tenant, account.Id);
                membership.Activate();
                seed.TenantMemberships.Add(membership);
                await seed.SaveChangesAsync();
            }

            await new BootstrapTenantAccessHandler(seed).HandleAsync(new BootstrapTenantAccessCommand(tenant, adminPrincipal, Guid.NewGuid()));
            await AccessActionCatalogSeeder.EnsureSeededAsync(seed, AccessActionCatalog.All.Concat(CrmSettingsActions.Select(
                action => new ActionRegistryDescriptor(action, "CRM", "CrmSettings"))));
            await seed.Database.ExecuteSqlInterpolatedAsync($"UPDATE access.roles SET origin_module_key = NULL, origin_version = NULL WHERE tenant_id = {tenant.Value} AND key = 'tenant_administrator'");
            await seed.Database.ExecuteSqlInterpolatedAsync($"UPDATE access.permission_sets SET origin_module_key = NULL, origin_version = NULL WHERE tenant_id = {tenant.Value} AND key = 'tenant_administration'");
        }

        await using (var ensure = fixture.CreateAdminContext())
        {
            var handler = new EnsureTenantAdministratorActionsHandler(ensure);
            Assert.True(await handler.HandleAsync(new EnsureTenantAdministratorActionsCommand(tenant, adminPrincipal, CrmSettingsActions, Guid.NewGuid())));
            Assert.False(await handler.HandleAsync(new EnsureTenantAdministratorActionsCommand(tenant, adminPrincipal, CrmSettingsActions, Guid.NewGuid())));
            Assert.False(await handler.HandleAsync(new EnsureTenantAdministratorActionsCommand(tenant, ordinaryPrincipal, CrmSettingsActions, Guid.NewGuid())));
        }

        await using var verify = fixture.CreateAdminContext();
        var roleId = await verify.Roles.Where(role => role.TenantId == tenant && role.Key == "tenant_administrator")
            .Select(role => role.Id).SingleAsync();
        var permissionSetId = await verify.RolePermissionSets.Where(link => link.TenantId == tenant && link.RoleId == roleId)
            .Join(verify.PermissionSets, link => link.PermissionSetId, set => set.Id, (_, set) => new { set.Id, set.Key })
            .Where(set => set.Key == "tenant_administration").Select(set => set.Id).SingleAsync();
        Assert.Equal(CrmSettingsActions.Order(), await verify.PermissionSetItems.Where(item => item.TenantId == tenant
                && item.PermissionSetId == permissionSetId && CrmSettingsActions.Contains(item.ActionKey))
            .Select(item => item.ActionKey).Order().ToArrayAsync());
        Assert.Equal(1, await verify.EvidenceRecords.CountAsync(record => record.TenantId == tenant && record.Action == "TenantAdministrator.CapabilitiesAdded"));
        Assert.Equal(1, await verify.OutboxMessages.CountAsync(message => message.TenantId == tenant
            && message.EventType == "enterprise.access.tenant_administrator.capabilities_updated.v1"));
        Assert.Equal(1, await verify.IdempotencyRecords.CountAsync(record => record.TenantId == tenant
            && record.Operation == "EnsureTenantAdministratorActions"));
        Assert.Equal(1, await verify.TenantAccessStates.Where(state => state.TenantId == tenant).Select(state => state.Revision).SingleAsync());
    }
}
