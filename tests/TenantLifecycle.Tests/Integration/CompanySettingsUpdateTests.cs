using Contracts;
using Microsoft.EntityFrameworkCore;
using TenantLifecycle.Application;
using TenantLifecycle.Domain;
using TenantLifecycle.Persistence;

namespace TenantLifecycle.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class CompanySettingsUpdateTests(PostgresFixture fixture)
{
    private static readonly PrincipalRef Principal = new("test", "administrator");

    [Fact]
    public async Task Update_commits_profile_outbox_and_idempotency_record_once_then_replays()
    {
        var tenant = new TenantId(82001);
        await InsertAsync(tenant);
        var command = Command(tenant, "same-key", 1, "Updated company");

        var first = await UpdateAsync(command);
        var replay = await UpdateAsync(command);

        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        Assert.Equal(2, first.Settings.RowVersion);
        await using var admin = PostgresFixture.CreateContext(fixture.AdminConnectionString);
        Assert.Equal(2, (await admin.TenantProfiles.SingleAsync(profile => profile.TenantId == tenant)).RowVersion);
        Assert.Single(await admin.OutboxMessages.Where(message => message.TenantId == tenant).ToListAsync());
        Assert.Single(await admin.IdempotencyRecords.Where(record => record.TenantId == tenant).ToListAsync());
    }

    [Fact]
    public async Task Update_rejects_a_stale_version()
    {
        var tenant = new TenantId(82002);
        await InsertAsync(tenant);
        await UpdateAsync(Command(tenant, "first", 1, "First update"));

        await Assert.ThrowsAsync<CompanySettingsConcurrencyConflictException>(() =>
            UpdateAsync(Command(tenant, "stale", 1, "Stale update")));
    }

    private async Task<UpdateCompanySettingsResult> UpdateAsync(UpdateCompanySettingsCommand command)
    {
        await using var context = PostgresFixture.CreateContext(fixture.AdminConnectionString);
        return await new UpdateCompanySettingsHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command);
    }

    private async Task InsertAsync(TenantId tenant)
    {
        await using var context = PostgresFixture.CreateContext(fixture.AdminConnectionString);
        context.TenantProfiles.Add(TenantProfile.Provision(tenant, Details("Initial company")));
        await context.SaveChangesAsync();
    }

    private static UpdateCompanySettingsCommand Command(TenantId tenant, string key, long version, string displayName) =>
        new(tenant, Principal, Details(displayName), version, key, Guid.NewGuid());

    private static TenantProfileDetails Details(string displayName) =>
        new(displayName, null, null, null, null, null, null, "Europe/Istanbul", "TRY");
}
