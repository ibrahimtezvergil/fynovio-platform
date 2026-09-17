using Access.Domain.Authorization;
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
}
