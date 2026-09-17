using CRM.Persistence;
using MasterData.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace CRM.Tests.Integration;

/// <summary>CRM_Phase1_Test_Coverage_Verification_Report.pdf F-06/F-07: a database that only
/// ever migrates a fresh Testcontainers instance straight to the tip (PostgresFixture) can
/// never prove a migration's *upgrade* behavior against pre-existing rows — only that the final
/// schema shape is reachable. This fixture migrates to the last migration before
/// `RenameOpportunityLifecycle`, seeds rows in the legacy shape by hand (old column names, old
/// status vocabulary, a real `crm.parties` row), then migrates the rest of the way to the tip —
/// a deliberately separate container from <see cref="PostgresFixture"/> because it manipulates
/// migration history directly.</summary>
public sealed class LegacyMigrationFixture : IAsyncLifetime
{
    private const string PreRenameMigration = "20260916081636_EnableRowLevelSecurity";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("fynovio_platform_legacy_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string AdminConnectionString => _container.GetConnectionString();

    /// <summary>The tenant every seeded legacy row belongs to, so tests can filter without
    /// depending on row order.</summary>
    public Contracts.TenantId Tenant { get; private set; }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // MasterData migrates straight to the tip: it has no legacy shape of its own to seed
        // against here, and BackfillMasterDataParties (a CRM migration) needs masterdata.parties
        // to already exist.
        await using (var masterDataContext = CreateMasterDataContext())
            await masterDataContext.Database.MigrateAsync();

        await using (var crmContext = CreateCrmContext())
        {
            var migrator = crmContext.GetInfrastructure().GetRequiredService<IMigrator>();
            await migrator.MigrateAsync(PreRenameMigration);
        }

        Tenant = new Contracts.TenantId(Random.Shared.NextInt64(100_000, 999_999));
        await SeedLegacyRowsAsync();

        await using (var crmContext = CreateCrmContext())
            await crmContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public CrmDbContext CreateCrmContext() => PostgresFixture.CreateContext(AdminConnectionString);

    public MasterDataDbContext CreateMasterDataContext() => PostgresFixture.CreateMasterDataContext(AdminConnectionString);

    /// <summary>Legacy shape as of `EnableRowLevelSecurity` (InitialCrmSchema +
    /// FixOpportunityAssignedPrincipalIndex + FixCancelExpiryCheck): `parties.creation_source`
    /// is required; `opportunities` still has `party_id`/`cancel_reason`/`offer_date`/
    /// `sale_date`/`cancel_date` and the four-value `status` CHECK
    /// (`ck_opportunities_expiry_required_once_offered`: `status NOT IN ('offered','completed')
    /// OR expiry_date IS NOT NULL`). One party, four opportunities — one per legacy status,
    /// each satisfying whatever that status's CHECKs require.</summary>
    private async Task SeedLegacyRowsAsync()
    {
        await using var context = CreateCrmContext();

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO crm.parties (tenant_id, name, surname, phone, email, creation_source, created_at, updated_at)
            VALUES ({Tenant.Value}, 'Legacy Acme', 'Corp', '+90 555 000 00 00', 'legacy@acme.test', 'manual', now(), now());
            """);

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO crm.opportunities
                (tenant_id, party_id, assigned_principal_issuer, assigned_principal_subject, status,
                 currency, estimated_amount, row_version, created_at, updated_at)
            SELECT {Tenant.Value}, p.id, 'https://idp.local', 'legacy-waiting', 'waiting',
                   'TRY', 1000.00, 1, now(), now()
            FROM crm.parties p WHERE p.tenant_id = {Tenant.Value};
            """);

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO crm.opportunities
                (tenant_id, party_id, assigned_principal_issuer, assigned_principal_subject, status,
                 currency, estimated_amount, row_version, expiry_date, created_at, updated_at)
            SELECT {Tenant.Value}, p.id, 'https://idp.local', 'legacy-offered', 'offered',
                   'TRY', 1000.00, 1, now() + interval '7 days', now(), now()
            FROM crm.parties p WHERE p.tenant_id = {Tenant.Value};
            """);

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO crm.opportunities
                (tenant_id, party_id, assigned_principal_issuer, assigned_principal_subject, status,
                 currency, estimated_amount, row_version, expiry_date, sale_date, created_at, updated_at)
            SELECT {Tenant.Value}, p.id, 'https://idp.local', 'legacy-completed', 'completed',
                   'TRY', 1000.00, 1, now() + interval '7 days', now(), now(), now()
            FROM crm.parties p WHERE p.tenant_id = {Tenant.Value};
            """);

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO crm.opportunities
                (tenant_id, party_id, assigned_principal_issuer, assigned_principal_subject, status,
                 currency, estimated_amount, row_version, cancel_date, cancel_reason, created_at, updated_at)
            SELECT {Tenant.Value}, p.id, 'https://idp.local', 'legacy-canceled', 'canceled',
                   'TRY', 1000.00, 1, now(), 'bütçe iptal oldu', now(), now()
            FROM crm.parties p WHERE p.tenant_id = {Tenant.Value};
            """);
    }
}
