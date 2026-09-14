using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CRM.Persistence;

/// <summary>Design-time factory so `dotnet ef migrations add` works standalone from this
/// module project before Host wires up real DI. Not used at runtime — Host configures
/// CrmDbContext through AddDbContext with the real connection string and interceptors.</summary>
public sealed class CrmDbContextFactory : IDesignTimeDbContextFactory<CrmDbContext>
{
    public CrmDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("FYNOVIO_CRM_CONNECTION_STRING")
            ?? "Host=localhost;Database=fynovio_platform;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<CrmDbContext>();
        optionsBuilder
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CrmDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new RowVersionInterceptor());

        return new CrmDbContext(optionsBuilder.Options);
    }
}
