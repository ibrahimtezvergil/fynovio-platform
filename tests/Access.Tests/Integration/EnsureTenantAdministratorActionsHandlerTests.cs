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

    private static readonly ModuleCapabilityCatalog Catalog = new([new ModuleCapabilityManifest("crm", "CRM", 1,
        [new PermissionSetTemplate("crm_settings_manage", "CRM settings", [.. CrmSettingsActions.Select(action => new PermissionSetTemplateItem(action))])],
        [])]);

    [Fact]
    public async Task Adds_only_the_requested_capabilities_to_an_existing_admin_and_is_idempotent()
    {
        var tenant = TestData.NextTenant();
        var principals = await SeedTenantAsync(tenant, enableCrm: true);

        await using (var ensure = fixture.CreateAdminContext())
        {
            var handler = new EnsureTenantAdministratorActionsHandler(ensure, Catalog);
            Assert.True(await handler.HandleAsync(new EnsureTenantAdministratorActionsCommand(tenant, principals.Admin, CrmSettingsActions, Guid.NewGuid())));
            Assert.False(await handler.HandleAsync(new EnsureTenantAdministratorActionsCommand(tenant, principals.Admin, CrmSettingsActions, Guid.NewGuid())));
            Assert.False(await handler.HandleAsync(new EnsureTenantAdministratorActionsCommand(tenant, principals.Member, CrmSettingsActions, Guid.NewGuid())));
        }

        await using var verify = fixture.CreateAdminContext();
        Assert.Equal(CrmSettingsActions.Order(), await AdministratorActionsAsync(verify, tenant, CrmSettingsActions));
        Assert.Equal(1, await verify.EvidenceRecords.CountAsync(record => record.TenantId == tenant && record.Action == "TenantAdministrator.CapabilitiesAdded"));
        Assert.Equal(1, await verify.OutboxMessages.CountAsync(message => message.TenantId == tenant
            && message.EventType == "enterprise.access.tenant_administrator.capabilities_updated.v1"));
        Assert.Equal(1, await verify.IdempotencyRecords.CountAsync(record => record.TenantId == tenant
            && record.Operation == "EnsureTenantAdministratorActions"));
        Assert.Equal(1, await verify.TenantAccessStates.Where(state => state.TenantId == tenant).Select(state => state.Revision).SingleAsync());
    }

    [Fact]
    public async Task Never_grants_actions_of_a_module_the_tenant_has_not_enabled()
    {
        var tenant = TestData.NextTenant();
        var principals = await SeedTenantAsync(tenant, enableCrm: false);

        await using (var ensure = fixture.CreateAdminContext())
            Assert.False(await new EnsureTenantAdministratorActionsHandler(ensure, Catalog).HandleAsync(
                new EnsureTenantAdministratorActionsCommand(tenant, principals.Admin, CrmSettingsActions, Guid.NewGuid())));

        await using var verify = fixture.CreateAdminContext();
        Assert.Empty(await AdministratorActionsAsync(verify, tenant, CrmSettingsActions));
    }

    private static async Task<string[]> AdministratorActionsAsync(AccessDbContext verify, TenantId tenant, string[] actions)
    {
        var roleId = await verify.Roles.Where(role => role.TenantId == tenant && role.Key == "tenant_administrator")
            .Select(role => role.Id).SingleAsync();
        var permissionSetId = await verify.RolePermissionSets.Where(link => link.TenantId == tenant && link.RoleId == roleId)
            .Join(verify.PermissionSets, link => link.PermissionSetId, set => set.Id, (_, set) => new { set.Id, set.Key })
            .Where(set => set.Key == "tenant_administration").Select(set => set.Id).SingleAsync();
        return await verify.PermissionSetItems.Where(item => item.TenantId == tenant
                && item.PermissionSetId == permissionSetId && actions.Contains(item.ActionKey))
            .Select(item => item.ActionKey).Order().ToArrayAsync();
    }

    private async Task<(PrincipalRef Admin, PrincipalRef Member)> SeedTenantAsync(TenantId tenant, bool enableCrm)
    {
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
            if (enableCrm)
            {
                seed.TenantModuleEnablements.Add(TenantModuleEnablement.Enable(tenant, "crm", 1, DateTimeOffset.UtcNow));
                await seed.SaveChangesAsync();
            }
        }

        return (adminPrincipal, ordinaryPrincipal);
    }
}


