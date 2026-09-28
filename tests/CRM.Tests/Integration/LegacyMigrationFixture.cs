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

    /// <summary>The last migration before `ck_opportunities_stage_required_once_open`
    /// (docs/plans/2026-09-27 Task 19) goes live. A real upgrade must run the
    /// `backfill-crm-pipelines` operator command (Task 18) between this point and the
    /// tip — the seeded `legacy-offered` row predates the "stage mandatory once open"
    /// invariant and has no pipeline stage of its own, exactly the scenario that command
    /// exists to fix.</summary>
    private const string PreStageRequiredCheckMigration = "20260928090207_AddOpportunityStageHistory";

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
        {
            var migrator = crmContext.GetInfrastructure().GetRequiredService<IMigrator>();
            await migrator.MigrateAsync(PreStageRequiredCheckMigration);
        }

        await BackfillLegacyOfferedStageAsync();

        await using (var crmContext = CreateCrmContext())
            await crmContext.Database.MigrateAsync();
    }

    /// <summary>Simulates the one-time `backfill-crm-pipelines` operator command (Task 18)
    /// against the `legacy-offered` row, which the real command would resolve the same way:
    /// no pipeline of its own, so it falls onto a pipeline's entry stage.</summary>
    private async Task BackfillLegacyOfferedStageAsync()
    {
        await using var context = CreateCrmContext();

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO crm.pipeline_definitions (tenant_id, name, created_at, updated_at)
            VALUES ({Tenant.Value}, 'Legacy Pipeline', now(), now());
            """);

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO crm.pipeline_definition_versions
                (tenant_id, pipeline_definition_id, version_number, created_at, status, published_at)
            SELECT {Tenant.Value}, d.id, 1, now(), 'Published', now()
            FROM crm.pipeline_definitions d
            WHERE d.tenant_id = {Tenant.Value} AND d.name = 'Legacy Pipeline';
            """);

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO crm.pipeline_stages
                (tenant_id, pipeline_definition_version_id, name, sort_order, created_at, is_entry, kind)
            SELECT {Tenant.Value}, v.id, 'Open', 0, now(), true, 'open'
            FROM crm.pipeline_definition_versions v
            JOIN crm.pipeline_definitions d ON d.id = v.pipeline_definition_id AND d.tenant_id = v.tenant_id
            WHERE v.tenant_id = {Tenant.Value} AND d.name = 'Legacy Pipeline';
            """);

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE crm.opportunities o
            SET pipeline_definition_version_id = v.id, pipeline_stage_id = s.id
            FROM crm.pipeline_definition_versions v
            JOIN crm.pipeline_definitions d ON d.id = v.pipeline_definition_id AND d.tenant_id = v.tenant_id
            JOIN crm.pipeline_stages s ON s.pipeline_definition_version_id = v.id AND s.tenant_id = v.tenant_id
            WHERE o.tenant_id = {Tenant.Value} AND o.assigned_principal_subject = 'legacy-offered'
              AND d.tenant_id = {Tenant.Value} AND d.name = 'Legacy Pipeline';
            """);
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
