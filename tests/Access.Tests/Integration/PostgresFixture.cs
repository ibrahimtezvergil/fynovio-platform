using Access.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Access.Tests.Integration;

/// <summary>Mirrors `tests/CRM.Tests/Integration/PostgresFixture.cs`. The container's
/// `postgres` user is superuser and bypasses RLS — isolation tests use
/// `RuntimeConnectionStringAsync()` instead (FF03).</summary>
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
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE ROLE fynovio_app LOGIN PASSWORD 'runtime' NOSUPERUSER NOBYPASSRLS;
                GRANT USAGE ON SCHEMA identity TO fynovio_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA identity TO fynovio_app;
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA identity TO fynovio_app;
                GRANT USAGE ON SCHEMA access TO fynovio_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA access TO fynovio_app;
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA access TO fynovio_app;
                REVOKE UPDATE, DELETE ON access.evidence_records FROM fynovio_app;
                """);
        }

        var builder = new NpgsqlConnectionStringBuilder(AdminConnectionString) { Username = "fynovio_app", Password = "runtime" };
        _runtimeConnectionString = builder.ConnectionString;
        return _runtimeConnectionString;
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateAdminContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public AccessDbContext CreateAdminContext() => CreateContext(AdminConnectionString);

    public static AccessDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AccessDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", AccessDbContext.AccessSchema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new RowVersionInterceptor())
            .Options;
        return new AccessDbContext(options);
    }
}
