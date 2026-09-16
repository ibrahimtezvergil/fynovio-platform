using Contracts;
using CRM.Domain;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CRM.Tests.Integration;

/// <summary>FF03 (doc 12): izolasyon, migration'ları çalıştıran superuser ile değil,
/// yetkisiz runtime rolüyle test edilir.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class TenantIsolationTests
{
    private readonly PostgresFixture _fixture;

    public TenantIsolationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Runtime_role_cannot_read_another_tenants_rows()
    {
        var (_, partyAId) = await SeedPartyAsync("A");
        var (tenantB, partyBId) = await SeedPartyAsync("B");

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenantB);

        var visible = await context.Parties.AsNoTracking().ToListAsync();

        Assert.Contains(visible, p => p.Id == partyBId);
        Assert.DoesNotContain(visible, p => p.Id == partyAId);
        Assert.All(visible, p => Assert.Equal(tenantB, p.TenantId));
    }

    [Fact]
    public async Task Runtime_role_cannot_read_a_guessed_id_from_another_tenant()
    {
        var (_, partyAId) = await SeedPartyAsync("A2");
        var (tenantB, _) = await SeedPartyAsync("B2");

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenantB);

        var guessed = await context.Parties.AsNoTracking().SingleOrDefaultAsync(p => p.Id == partyAId);

        Assert.Null(guessed);
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
        var singleConnectionPool = new NpgsqlConnectionStringBuilder(await _fixture.RuntimeConnectionStringAsync())
        {
            Pooling = true,
            MaxPoolSize = 1
        }.ConnectionString;

        await using (var first = PostgresFixture.CreateContext(singleConnectionPool))
        {
            await using var transaction = await first.Database.BeginTransactionAsync();
            await first.SetTenantContextAsync(tenantA);
            Assert.NotEmpty(await first.Parties.AsNoTracking().ToListAsync());
            await transaction.CommitAsync();
        }

        await using var second = PostgresFixture.CreateContext(singleConnectionPool);
        await using var secondTransaction = await second.Database.BeginTransactionAsync();

        var visible = await second.Parties.AsNoTracking().ToListAsync();

        Assert.Empty(visible);
    }

    [Fact]
    public async Task Runtime_role_cannot_write_a_row_for_another_tenant()
    {
        var (tenantA, _) = await SeedPartyAsync("E");
        var tenantB = TestData.NextTenant();

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenantB);

        context.Parties.Add(Party.Create(tenantA, "Sızdırma denemesi", PartyCreationSource.Manual));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, postgresException.SqlState);
    }

    [Fact]
    public async Task Tenant_context_requires_an_explicit_transaction()
    {
        var tenant = TestData.NextTenant();
        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SetTenantContextAsync(tenant));
    }

    [Fact]
    public async Task Runtime_role_cannot_update_or_delete_evidence()
    {
        var tenant = TestData.NextTenant();
        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenant);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlRawAsync("DELETE FROM crm.evidence_records"));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }

    private async Task<(TenantId TenantId, long PartyId)> SeedPartyAsync(string name)
    {
        var tenant = TestData.NextTenant();
        await using var context = _fixture.CreateAdminContext();
        var party = Party.Create(tenant, name, PartyCreationSource.Manual);
        context.Parties.Add(party);
        await context.SaveChangesAsync();
        return (tenant, party.Id);
    }
}
