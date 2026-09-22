using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TenantLifecycle.Persistence;

public sealed class TenantLifecycleDbContextFactory : IDesignTimeDbContextFactory<TenantLifecycleDbContext>
{
    private const string DesignTimeConnectionString = "Host=localhost;Database=fynovio_platform;Username=postgres;Password=postgres";

    public TenantLifecycleDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TenantLifecycleDbContext>()
            .UseNpgsql(DesignTimeConnectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", TenantLifecycleDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new TenantLifecycleDbContext(options);
    }
}
