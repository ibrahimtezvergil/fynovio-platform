using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Messaging.Persistence;

public sealed class MessagingDbContextFactory : IDesignTimeDbContextFactory<MessagingDbContext>
{
    private const string DesignTimeConnectionString = "Host=localhost;Database=fynovio_platform;Username=postgres;Password=postgres";

    public MessagingDbContext CreateDbContext(string[] args) => Create(DesignTimeConnectionString);

    public static MessagingDbContext Create(string connectionString)
    {
        var options = new DbContextOptionsBuilder<MessagingDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", MessagingDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new MessagingDbContext(options);
    }
}
