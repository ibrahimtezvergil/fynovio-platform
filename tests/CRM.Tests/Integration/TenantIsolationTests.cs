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
        var (_, opportunityAId) = await SeedOpportunityAsync("A");
        var (tenantB, opportunityBId) = await SeedOpportunityAsync("B");

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenantB);

        var visible = await context.Opportunities.AsNoTracking().ToListAsync();

        Assert.Contains(visible, o => o.Id == opportunityBId);
        Assert.DoesNotContain(visible, o => o.Id == opportunityAId);
        Assert.All(visible, o => Assert.Equal(tenantB, o.TenantId));
    }

    [Fact]
    public async Task Runtime_role_cannot_read_a_guessed_id_from_another_tenant()
    {
        var (_, opportunityAId) = await SeedOpportunityAsync("A2");
        var (tenantB, _) = await SeedOpportunityAsync("B2");

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenantB);

        var guessed = await context.Opportunities.AsNoTracking().SingleOrDefaultAsync(o => o.Id == opportunityAId);

        Assert.Null(guessed);
    }

    [Fact]
    public async Task Runtime_role_sees_nothing_without_a_tenant_context()
    {
        await SeedOpportunityAsync("C");

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();

        var visible = await context.Opportunities.AsNoTracking().ToListAsync();

        Assert.Empty(visible);
    }

    [Fact]
    public async Task Tenant_context_does_not_survive_on_a_pooled_connection()
    {
        var (tenantA, _) = await SeedOpportunityAsync("D");
        var singleConnectionPool = new NpgsqlConnectionStringBuilder(await _fixture.RuntimeConnectionStringAsync())
        {
            Pooling = true,
            MaxPoolSize = 1
        }.ConnectionString;

        await using (var first = PostgresFixture.CreateContext(singleConnectionPool))
        {
            await using var transaction = await first.Database.BeginTransactionAsync();
            await first.SetTenantContextAsync(tenantA);
            Assert.NotEmpty(await first.Opportunities.AsNoTracking().ToListAsync());
            await transaction.CommitAsync();
        }

        await using var second = PostgresFixture.CreateContext(singleConnectionPool);
        await using var secondTransaction = await second.Database.BeginTransactionAsync();

        var visible = await second.Opportunities.AsNoTracking().ToListAsync();

        Assert.Empty(visible);
    }

    [Fact]
    public async Task Runtime_role_cannot_write_a_row_for_another_tenant()
    {
        var (tenantA, _) = await SeedOpportunityAsync("E");
        var tenantB = TestData.NextTenant();

        await using var masterDataContext = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(masterDataContext, tenantA, "Sızdırma denemesi");

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenantB);

        context.Opportunities.Add(Opportunity.Create(tenantA, partyRef, TestData.Seller, "TRY", 100m));

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

    private async Task<(TenantId TenantId, long OpportunityId)> SeedOpportunityAsync(string partyName)
    {
        var tenant = TestData.NextTenant();
        await using var masterDataContext = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(masterDataContext, tenant, partyName);

        await using var context = _fixture.CreateAdminContext();
        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        context.Opportunities.Add(opportunity);
        await context.SaveChangesAsync();
        return (tenant, opportunity.Id);
    }
}
