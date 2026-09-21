using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Collaboration.Persistence;

public sealed class CollaborationDbContextFactory : IDesignTimeDbContextFactory<CollaborationDbContext>
{
    private const string DesignTimeConnectionString = "Host=localhost;Database=fynovio_platform;Username=postgres;Password=postgres";

    public CollaborationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CollaborationDbContext>()
            .UseNpgsql(DesignTimeConnectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CollaborationDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new CollaborationDbContext(options);
    }
}
