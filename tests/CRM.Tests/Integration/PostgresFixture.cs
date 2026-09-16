using CRM.Persistence;
using MasterData.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace CRM.Tests.Integration;

/// <summary>Gerçek PostgreSQL üzerinde migration'ları uygular (AGENTS.md: paylaşılan dev
/// veritabanı yok, Testcontainers). Konteynerin `postgres` kullanıcısı superuser'dır ve
/// RLS'i bypass eder — izolasyon testleri <see cref="RuntimeConnectionStringAsync"/> ile
/// yetkisiz rolle çalışır (bkz. TenantIsolationTests).</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("fynovio_platform_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private string? _runtimeConnectionString;

    public string AdminConnectionString => _container.GetConnectionString();

    /// <summary>Migration'ları uygulayan superuser'dan ayrı, RLS'e tabi rol (FF03).
    /// Superuser ve BYPASSRLS rolleri RLS'i her koşulda atlar; izolasyon yalnızca bu rolle
    /// test edilebilir.</summary>
    public async Task<string> RuntimeConnectionStringAsync()
    {
        if (_runtimeConnectionString is not null)
            return _runtimeConnectionString;

        await using (var context = CreateAdminContext())
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE ROLE fynovio_app LOGIN PASSWORD 'runtime' NOSUPERUSER NOBYPASSRLS;
                GRANT USAGE ON SCHEMA crm TO fynovio_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA crm TO fynovio_app;
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA crm TO fynovio_app;
                REVOKE UPDATE, DELETE ON crm.evidence_records FROM fynovio_app;
                GRANT USAGE ON SCHEMA masterdata TO fynovio_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA masterdata TO fynovio_app;
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA masterdata TO fynovio_app;
                REVOKE UPDATE, DELETE ON masterdata.evidence_records FROM fynovio_app;
                """);
        }

        var builder = new NpgsqlConnectionStringBuilder(AdminConnectionString)
        {
            Username = "fynovio_app",
            Password = "runtime"
        };

        _runtimeConnectionString = builder.ConnectionString;
        return _runtimeConnectionString;
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var crmContext = CreateAdminContext();
        await crmContext.Database.MigrateAsync();
        await using var masterDataContext = CreateMasterDataContext();
        await masterDataContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public CrmDbContext CreateAdminContext() => CreateContext(AdminConnectionString);

    public MasterDataDbContext CreateMasterDataContext() => CreateMasterDataContext(AdminConnectionString);

    public static CrmDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CrmDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new CrmDbContext(options);
    }

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
}
