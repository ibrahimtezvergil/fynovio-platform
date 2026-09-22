using Contracts;
using Microsoft.EntityFrameworkCore;
using TenantLifecycle.Domain;
using TenantLifecycle.Persistence;

namespace TenantLifecycle.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class RowLevelSecurityTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Runtime_role_reads_only_the_tenant_selected_by_the_transaction_local_guc()
    {
        var first = new TenantId(81001);
        var second = new TenantId(81002);
        await InsertAsync(first, "First company");
        await InsertAsync(second, "Second company");

        var runtime = await fixture.RuntimeConnectionStringAsync();
        await using var context = PostgresFixture.CreateContext(runtime);
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(first);

        var visible = await context.TenantProfiles.AsNoTracking().Select(profile => profile.DisplayName).ToListAsync();

        Assert.Equal(["First company"], visible);
    }

    [Fact]
    public async Task Runtime_role_cannot_insert_a_profile_for_another_tenant()
    {
        var visibleTenant = new TenantId(81003);
        await InsertAsync(visibleTenant, "Visible company");

        await using var context = PostgresFixture.CreateContext(await fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(visibleTenant);
        context.TenantProfiles.Add(TenantProfile.Provision(new TenantId(81004), new TenantProfileDetails(
            "Hidden company", null, null, null, null, null, null, "Europe/Istanbul", "TRY")));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private async Task InsertAsync(TenantId tenantId, string displayName)
    {
        await using var context = PostgresFixture.CreateContext(fixture.AdminConnectionString);
        context.TenantProfiles.Add(TenantProfile.Provision(tenantId, new TenantProfileDetails(
            displayName, null, null, null, null, null, null, "Europe/Istanbul", "TRY")));
        await context.SaveChangesAsync();
    }
}
