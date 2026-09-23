using Access.Domain.Authorization;
using Access.Domain.Identity;
using Access.Domain.Authentication;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Tests.Integration;

/// <summary>FF03: run as the unprivileged runtime role, never the superuser that runs
/// migrations — superusers and table owners bypass RLS regardless of policy.</summary>
public sealed class AccessRlsTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public AccessRlsTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Runtime_role_cannot_read_or_insert_another_tenants_invitation_delivery()
    {
        var home = new TenantId(1901);
        var foreign = new TenantId(1902);
        var invitationId = Guid.NewGuid();
        await using (var admin = _fixture.CreateAdminContext())
        {
            admin.InvitationDeliveries.Add(InvitationDelivery.Create(foreign, invitationId, "protected", DateTimeOffset.UtcNow));
            await admin.SaveChangesAsync();
        }

        var runtimeConnectionString = await _fixture.RuntimeConnectionStringAsync();
        await using var runtime = PostgresFixture.CreateContext(runtimeConnectionString);
        await using var transaction = await runtime.Database.BeginTransactionAsync();
        await runtime.SetTenantContextAsync(home);
        Assert.False(await runtime.InvitationDeliveries.AnyAsync(delivery => delivery.InvitationId == invitationId));
        runtime.InvitationDeliveries.Add(InvitationDelivery.Create(foreign, Guid.NewGuid(), "protected", DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<DbUpdateException>(() => runtime.SaveChangesAsync());
    }

    [Fact]
    public async Task Runtime_role_cannot_see_another_tenants_roles()
    {
        var tenantA = new TenantId(1);
        var tenantB = new TenantId(2);

        await using (var admin = _fixture.CreateAdminContext())
        {
            admin.Roles.Add(Role.Create(tenantA, "role_a", "Role A"));
            admin.Roles.Add(Role.Create(tenantB, "role_b", "Role B"));
            await admin.SaveChangesAsync();
        }

        var runtimeConnectionString = await _fixture.RuntimeConnectionStringAsync();
        await using var runtime = PostgresFixture.CreateContext(runtimeConnectionString);

        await using var transaction = await runtime.Database.BeginTransactionAsync();
        await runtime.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('app.tenant_id', {tenantA.Value.ToString()}, true)");

        var visibleRoles = await runtime.Roles.Select(r => r.Key).ToListAsync();

        Assert.Contains("role_a", visibleRoles);
        Assert.DoesNotContain("role_b", visibleRoles);
    }

    [Fact]
    public async Task Runtime_role_without_tenant_context_sees_nothing()
    {
        await using (var admin = _fixture.CreateAdminContext())
        {
            admin.Roles.Add(Role.Create(new TenantId(3), "role_c", "Role C"));
            await admin.SaveChangesAsync();
        }

        var runtimeConnectionString = await _fixture.RuntimeConnectionStringAsync();
        await using var runtime = PostgresFixture.CreateContext(runtimeConnectionString);

        await using var transaction = await runtime.Database.BeginTransactionAsync();
        // No set_config call — app.tenant_id is unset, NULLIF(...) comparison never matches.

        var visibleRoles = await runtime.Roles.Where(r => r.Key == "role_c").ToListAsync();

        Assert.Empty(visibleRoles);
    }

    /// <summary>C5 (INSERT): `tenant_access_state` has no foreign keys, so it isolates
    /// the `WITH CHECK` clause cleanly — inserting a row whose `tenant_id` doesn't
    /// match the session's `app.tenant_id` must be rejected by the policy itself.</summary>
    [Fact]
    public async Task Runtime_role_cannot_insert_tenant_access_state_for_another_tenant()
    {
        var sessionTenant = new TenantId(10);
        var foreignTenant = new TenantId(11);

        var runtimeConnectionString = await _fixture.RuntimeConnectionStringAsync();
        await using var runtime = PostgresFixture.CreateContext(runtimeConnectionString);

        await using var transaction = await runtime.Database.BeginTransactionAsync();
        await runtime.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('app.tenant_id', {sessionTenant.Value.ToString()}, true)");

        runtime.TenantAccessStates.Add(TenantAccessState.Initialize(foreignTenant));
        await Assert.ThrowsAsync<DbUpdateException>(() => runtime.SaveChangesAsync());
    }

    /// <summary>C5 (UPDATE): the `USING` clause filters the foreign-tenant row out
    /// before the statement ever reaches it — this is 0 rows affected, not an
    /// exception, which is a different (and easier to get wrong) assurance than the
    /// INSERT case.</summary>
    [Fact]
    public async Task Runtime_role_cannot_update_another_tenants_tenant_access_state()
    {
        var sessionTenant = new TenantId(12);
        var foreignTenant = new TenantId(13);

        await using (var admin = _fixture.CreateAdminContext())
        {
            admin.TenantAccessStates.Add(TenantAccessState.Initialize(foreignTenant));
            await admin.SaveChangesAsync();
        }

        var runtimeConnectionString = await _fixture.RuntimeConnectionStringAsync();
        await using (var runtime = PostgresFixture.CreateContext(runtimeConnectionString))
        {
            await using var transaction = await runtime.Database.BeginTransactionAsync();
            await runtime.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('app.tenant_id', {sessionTenant.Value.ToString()}, true)");

            var affectedRows = await runtime.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE access.tenant_access_state SET revision = revision + 1 WHERE tenant_id = {foreignTenant.Value}");

            Assert.Equal(0, affectedRows);
            await transaction.CommitAsync();
        }

        await using var verify = _fixture.CreateAdminContext();
        var revision = await verify.TenantAccessStates.Where(s => s.TenantId == foreignTenant).Select(s => s.Revision).SingleAsync();
        Assert.Equal(0, revision);
    }

    /// <summary>C5 (DELETE), on the table the review specifically called out as more
    /// security-relevant than Roles: a RoleAssignment directly determines effective
    /// authorization, so a cross-tenant DELETE must be filtered out, not merely
    /// rejected on write.</summary>
    [Fact]
    public async Task Runtime_role_cannot_delete_another_tenants_role_assignment()
    {
        var sessionTenant = new TenantId(14);
        var foreignTenant = new TenantId(15);
        long assignmentId;

        await using (var admin = _fixture.CreateAdminContext())
        {
            var role = Role.Create(foreignTenant, "role_d", "Role D");
            admin.Roles.Add(role);
            var account = Account.Create("role-d-account@test.local", "role-d-subject");
            admin.Accounts.Add(account);
            await admin.SaveChangesAsync();

            var assignment = RoleAssignment.Grant(foreignTenant, account.Id, role.Id, account.Id, RoleAssignment.SourceManual);
            admin.RoleAssignments.Add(assignment);
            await admin.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        var runtimeConnectionString = await _fixture.RuntimeConnectionStringAsync();
        await using (var runtime = PostgresFixture.CreateContext(runtimeConnectionString))
        {
            await using var transaction = await runtime.Database.BeginTransactionAsync();
            await runtime.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('app.tenant_id', {sessionTenant.Value.ToString()}, true)");

            var affectedRows = await runtime.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM access.role_assignments WHERE id = {assignmentId}");

            Assert.Equal(0, affectedRows);
            await transaction.CommitAsync();
        }

        await using var verify = _fixture.CreateAdminContext();
        Assert.True(await verify.RoleAssignments.AnyAsync(a => a.Id == assignmentId));
    }
}
