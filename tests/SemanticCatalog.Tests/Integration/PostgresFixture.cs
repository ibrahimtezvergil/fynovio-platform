using Microsoft.EntityFrameworkCore;
using Npgsql;
using SemanticCatalog.Persistence;
using Testcontainers.PostgreSql;

namespace SemanticCatalog.Tests.Integration;

/// <summary>The `semantic` schema on real PostgreSQL (no shared dev database: Testcontainers). The container's
/// `postgres` user is a superuser and bypasses RLS — use it only to migrate and seed; anything that proves isolation or
/// the reader runs as <see cref="RuntimeConnectionStringAsync"/>, a role bound by RLS.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("fynovio_platform_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private string? _runtimeConnectionString;

    public string AdminConnectionString => _container.GetConnectionString();

    public async Task<string> RuntimeConnectionStringAsync()
    {
        if (_runtimeConnectionString is not null)
            return _runtimeConnectionString;

        await using (var context = CreateAdminContext())
        {
            // The catalog's grants exactly as scripts/create-runtime-role.sql gives them.
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE ROLE fynovio_app LOGIN PASSWORD 'runtime' NOSUPERUSER NOBYPASSRLS;
                GRANT USAGE ON SCHEMA semantic TO fynovio_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA semantic TO fynovio_app;
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA semantic TO fynovio_app;
                REVOKE UPDATE, DELETE ON semantic.evidence_records FROM fynovio_app;
                """);
        }

        _runtimeConnectionString = new NpgsqlConnectionStringBuilder(AdminConnectionString) { Username = "fynovio_app", Password = "runtime" }.ConnectionString;
        return _runtimeConnectionString;
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateAdminContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public SemanticCatalogDbContext CreateAdminContext() => CreateContext(AdminConnectionString);

    public static SemanticCatalogDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<SemanticCatalogDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", SemanticCatalogDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new SemanticCatalogDbContext(options);
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
