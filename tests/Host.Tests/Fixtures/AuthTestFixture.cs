using Access.Persistence;
using Collaboration.Persistence;
using CRM.Persistence;
using MasterData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Host.Tests.Fixtures;

/// <summary>Shared test fixture for authentication tests. Provides database context creation,
/// migration, and runtime role setup — extracted from OpportunityEndpointsTests to avoid duplication.</summary>
internal static class AuthTestFixture
{
    /// <summary>Create contexts for each database, used by tests for seeding and verification.</summary>
    public static MasterDataDbContext CreateMasterDataContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<MasterDataDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", MasterDataDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new MasterDataDbContext(options);
    }

    public static CrmDbContext CreateCrmContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CrmDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new CrmDbContext(options);
    }

    public static CollaborationDbContext CreateCollaborationContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<CollaborationDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CollaborationDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new CollaborationDbContext(options);
    }

    public static AccessDbContext CreateAccessContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AccessDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", AccessDbContext.AccessSchema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new RowVersionInterceptor())
            .Options;

        return new AccessDbContext(options);
    }

    /// <summary>Create unprivileged runtime role, matching scripts/create-runtime-role.sql.</summary>
    public static async Task<string> CreateRuntimeRoleAsync(string adminConnectionString)
    {
        await using var admin = CreateCrmContext(adminConnectionString);
        await admin.Database.ExecuteSqlRawAsync("""
            CREATE ROLE fynovio_app LOGIN PASSWORD 'runtime' NOSUPERUSER NOBYPASSRLS;

            GRANT USAGE ON SCHEMA crm TO fynovio_app;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA crm TO fynovio_app;
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA crm TO fynovio_app;
            REVOKE UPDATE, DELETE ON crm.evidence_records FROM fynovio_app;

            GRANT USAGE ON SCHEMA masterdata TO fynovio_app;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA masterdata TO fynovio_app;
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA masterdata TO fynovio_app;
            REVOKE UPDATE, DELETE ON masterdata.evidence_records FROM fynovio_app;

            GRANT USAGE ON SCHEMA collaboration TO fynovio_app;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA collaboration TO fynovio_app;
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA collaboration TO fynovio_app;

            GRANT USAGE ON SCHEMA tenant_lifecycle TO fynovio_app;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA tenant_lifecycle TO fynovio_app;
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA tenant_lifecycle TO fynovio_app;

            GRANT USAGE ON SCHEMA identity TO fynovio_app;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA identity TO fynovio_app;
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA identity TO fynovio_app;

            GRANT USAGE ON SCHEMA access TO fynovio_app;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA access TO fynovio_app;
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA access TO fynovio_app;
            REVOKE UPDATE, DELETE ON access.evidence_records FROM fynovio_app;
            """);

        var builder = new Npgsql.NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Username = "fynovio_app",
            Password = "runtime"
        };
        return builder.ConnectionString;
    }
}
