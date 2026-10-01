using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SemanticCatalog.Persistence;

/// <summary>Design-time factory so `dotnet ef migrations add` works from this project alone. Hardcodes the local
/// Postgres superuser: migrations need table-owner privileges to create tables and enable RLS, independent of the
/// runtime default in <see cref="SemanticCatalogConnectionString"/>.</summary>
public sealed class SemanticCatalogDbContextFactory : IDesignTimeDbContextFactory<SemanticCatalogDbContext>
{
    private const string DesignTimeConnectionString = "Host=localhost;Database=fynovio_platform;Username=postgres;Password=postgres";

    public SemanticCatalogDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SemanticCatalogDbContext>();
        optionsBuilder
            .UseNpgsql(DesignTimeConnectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", SemanticCatalogDbContext.Schema))
            .UseSnakeCaseNamingConvention();

        return new SemanticCatalogDbContext(optionsBuilder.Options);
    }
}
