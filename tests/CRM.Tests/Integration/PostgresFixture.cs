using CRM.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace CRM.Tests.Integration;

/// <summary>Gerçek PostgreSQL üzerinde migration'ları uygular (AGENTS.md: paylaşılan dev
/// veritabanı yok, Testcontainers). Konteynerin `postgres` kullanıcısı superuser'dır ve
/// RLS'i bypass eder — yetkisiz runtime rolüyle yapılan izolasyon testi Task 7'de.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("fynovio_platform_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string AdminConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateAdminContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public CrmDbContext CreateAdminContext() => CreateContext(AdminConnectionString);

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
}
