using Access.Application;
using Access.Domain.Authorization;
using Access.Domain.Identity;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Tests.Integration;

public sealed class BootstrapTenantAccessHandlerTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public BootstrapTenantAccessHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Bootstrap_creates_tenant_administrator_role_and_assignment()
    {
        var tenantId = new TenantId(500);
        var principal = new PrincipalRef("test-idp", $"bootstrap-admin-{Guid.NewGuid():N}");

        await using (var seed = _fixture.CreateAdminContext())
        {
            // Production reality: Host seeds the action registry at startup before
            // anything else runs. BootstrapTenantAccessHandler grants every catalog
            // action, so PermissionSetItem's FK to access.actions requires this row
            // to exist first — the FK violation this seeding fixes is real, not a
            // handler bug.
            await AccessActionCatalogSeeder.EnsureSeededAsync(seed, AccessActionCatalog.All);

            var account = Account.Create($"{principal.Subject}@test.local", principal.Subject);
            seed.Accounts.Add(account);
            await seed.SaveChangesAsync();
            seed.ExternalIdentities.Add(ExternalIdentity.Link(account.Id, principal));
            await seed.SaveChangesAsync();
        }

        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new BootstrapTenantAccessHandler(context);
            await handler.HandleAsync(new BootstrapTenantAccessCommand(tenantId, principal, Guid.NewGuid()));
        }

        await using (var verify = _fixture.CreateAdminContext())
        {
            var state = await verify.TenantAccessStates.SingleAsync(s => s.TenantId == tenantId);
            Assert.Equal(0, state.Revision);

            var assignment = await verify.RoleAssignments.SingleAsync(a => a.TenantId == tenantId);
            Assert.Equal("bootstrap", assignment.Source);

            var evidenceCount = await verify.EvidenceRecords.CountAsync(e => e.TenantId == tenantId);
            var outboxCount = await verify.OutboxMessages.CountAsync(m => m.TenantId == tenantId);
            Assert.Equal(1, evidenceCount);
            Assert.Equal(1, outboxCount);
        }
    }

    /// <summary>H8: bootstrap's Role/PermissionSet are instances of the
    /// "tenant_administrator" system template, materialized as tenant-owned rows —
    /// not a runtime-global role shared across tenants. Round 4 Decision A (system
    /// catalog: template -> tenant-local instance).</summary>
    [Fact]
    public async Task Bootstrap_role_and_permission_set_are_tenant_owned_system_template_instances()
    {
        var tenantId = new TenantId(502);
        var principal = new PrincipalRef("test-idp", $"bootstrap-admin-{Guid.NewGuid():N}");

        await using (var seed = _fixture.CreateAdminContext())
        {
            await AccessActionCatalogSeeder.EnsureSeededAsync(seed, AccessActionCatalog.All);

            var account = Account.Create($"{principal.Subject}@test.local", principal.Subject);
            seed.Accounts.Add(account);
            await seed.SaveChangesAsync();
            seed.ExternalIdentities.Add(ExternalIdentity.Link(account.Id, principal));
            await seed.SaveChangesAsync();
        }

        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new BootstrapTenantAccessHandler(context);
            await handler.HandleAsync(new BootstrapTenantAccessCommand(tenantId, principal, Guid.NewGuid()));
        }

        await using (var verify = _fixture.CreateAdminContext())
        {
            var role = await verify.Roles.SingleAsync(r => r.TenantId == tenantId);
            Assert.Equal(tenantId, role.TenantId);
            Assert.Equal(Role.OriginSystemTemplate, role.Origin);

            var permissionSet = await verify.PermissionSets.SingleAsync(p => p.TenantId == tenantId);
            Assert.Equal(tenantId, permissionSet.TenantId);
            Assert.Equal(PermissionSet.OriginSystemTemplate, permissionSet.Origin);
        }
    }

    [Fact]
    public async Task Bootstrap_twice_for_the_same_tenant_throws()
    {
        var tenantId = new TenantId(501);
        var principal = new PrincipalRef("test-idp", $"bootstrap-admin-{Guid.NewGuid():N}");

        await using (var seed = _fixture.CreateAdminContext())
        {
            await AccessActionCatalogSeeder.EnsureSeededAsync(seed, AccessActionCatalog.All);

            var account = Account.Create($"{principal.Subject}@test.local", principal.Subject);
            seed.Accounts.Add(account);
            await seed.SaveChangesAsync();
            seed.ExternalIdentities.Add(ExternalIdentity.Link(account.Id, principal));
            await seed.SaveChangesAsync();
        }

        await using (var first = _fixture.CreateAdminContext())
            await new BootstrapTenantAccessHandler(first).HandleAsync(new BootstrapTenantAccessCommand(tenantId, principal, Guid.NewGuid()));

        await using var second = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new BootstrapTenantAccessHandler(second).HandleAsync(new BootstrapTenantAccessCommand(tenantId, principal, Guid.NewGuid())));
    }
}
