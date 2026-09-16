using Contracts;
using MasterData.Domain;
using MasterData.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace MasterData.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class TenantIsolationTests
{
    private readonly PostgresFixture _fixture;

    public TenantIsolationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Runtime_role_cannot_read_another_tenants_rows()
    {
        var (tenantA, partyAId) = await SeedPartyAsync("A");
        var (tenantB, _) = await SeedPartyAsync("B");

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenantB);

        var visible = await context.Parties.AsNoTracking().ToListAsync();

        Assert.DoesNotContain(visible, p => p.Id == partyAId);
        Assert.All(visible, p => Assert.Equal(tenantB, p.TenantId));
        Assert.NotEqual(tenantA, tenantB);
    }

    [Fact]
    public async Task Runtime_role_cannot_read_a_guessed_id_from_another_tenant()
    {
        var (_, partyAId) = await SeedPartyAsync("A");
        var (tenantB, _) = await SeedPartyAsync("B");

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenantB);

        var found = await context.Parties.AsNoTracking().SingleOrDefaultAsync(p => p.Id == partyAId);

        Assert.Null(found);
    }

    [Fact]
    public async Task Runtime_role_sees_nothing_without_a_tenant_context()
    {
        await SeedPartyAsync("C");

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();

        var visible = await context.Parties.AsNoTracking().ToListAsync();

        Assert.Empty(visible);
    }

    [Fact]
    public async Task Tenant_context_does_not_survive_on_a_pooled_connection()
    {
        var (tenantA, _) = await SeedPartyAsync("D");

        var connectionString = await _fixture.RuntimeConnectionStringAsync();

        await using (var first = PostgresFixture.CreateContext(connectionString))
        {
            await using var transaction = await first.Database.BeginTransactionAsync();
            await first.SetTenantContextAsync(tenantA);
            await transaction.CommitAsync();
        }

        await using var second = PostgresFixture.CreateContext(connectionString);
        await using var secondTransaction = await second.Database.BeginTransactionAsync();

        var visible = await second.Parties.AsNoTracking().ToListAsync();

        Assert.Empty(visible);
    }

    [Fact]
    public async Task Runtime_role_cannot_write_a_row_for_another_tenant()
    {
        var tenantA = TestData.NextTenant();
        var tenantB = TestData.NextTenant();

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenantB);

        context.Parties.Add(Party.Create(tenantA, PartyType.Organization, "Sızdırma denemesi"));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal("42501", postgresException.SqlState);
    }

    [Fact]
    public async Task Tenant_context_requires_an_explicit_transaction()
    {
        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.SetTenantContextAsync(TestData.NextTenant()));
    }

    [Fact]
    public async Task Runtime_role_cannot_update_or_delete_evidence()
    {
        var tenant = TestData.NextTenant();

        await using (var admin = _fixture.CreateAdminContext())
        {
            admin.EvidenceRecords.Add(MasterData.Evidence.EvidenceRecord.Create(
                tenant, nameof(Party), 1, 1, TestData.Operator, "Party.Create", "{}", Guid.NewGuid()));
            await admin.SaveChangesAsync();
        }

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenant);

        var record = await context.EvidenceRecords.SingleAsync();
        context.EvidenceRecords.Remove(record);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal("42501", postgresException.SqlState);
    }

    private async Task<(TenantId TenantId, long PartyId)> SeedPartyAsync(string name)
    {
        var tenant = TestData.NextTenant();
        await using var context = _fixture.CreateAdminContext();
        var party = Party.Create(tenant, PartyType.Organization, name);
        context.Parties.Add(party);
        await context.SaveChangesAsync();
        return (tenant, party.Id);
    }
}
