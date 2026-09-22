using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using TenantLifecycle.Persistence;

namespace TenantLifecycle.Tests.Integration;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("fynovio_platform").WithUsername("postgres").WithPassword("postgres").Build();
    private string? _runtimeConnectionString;

    public string AdminConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateContext(AdminConnectionString);
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public async Task<string> RuntimeConnectionStringAsync()
    {
        if (_runtimeConnectionString is not null)
            return _runtimeConnectionString;

        var lines = await File.ReadAllLinesAsync(Path.Combine(LocateRoot(), "scripts", "create-runtime-role.sql"));
        var headerEnd = Array.FindIndex(lines, line => line.Contains("GRANT CONNECT ON DATABASE fynovio_platform", StringComparison.Ordinal));
        var sectionStart = Array.FindIndex(lines, line => line.StartsWith("-- TenantLifecycle module.", StringComparison.Ordinal));
        var sectionEnd = Array.FindIndex(lines, sectionStart + 1, line => line.StartsWith("-- Identity + Access modules", StringComparison.Ordinal));
        if (headerEnd < 0 || sectionStart < 0 || sectionEnd < 0)
            throw new InvalidOperationException("TenantLifecycle runtime-role grant section is missing.");

        await using (var context = CreateContext(AdminConnectionString))
            await context.Database.ExecuteSqlRawAsync(string.Join("\n", lines[..(headerEnd + 1)].Concat(lines[sectionStart..sectionEnd])));

        _runtimeConnectionString = new NpgsqlConnectionStringBuilder(AdminConnectionString)
        {
            Username = "fynovio_app",
            Password = "change-me"
        }.ConnectionString;
        return _runtimeConnectionString;
    }

    public static TenantLifecycleDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<TenantLifecycleDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", TenantLifecycleDbContext.Schema))
            .UseSnakeCaseNamingConvention().Options);

    private static string LocateRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "fynovio-platform.slnx")))
                return current.FullName;
        throw new InvalidOperationException("Repository root was not found.");
    }
}
