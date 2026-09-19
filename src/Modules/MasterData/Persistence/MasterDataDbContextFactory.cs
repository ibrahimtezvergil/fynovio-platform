using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MasterData.Persistence;

/// <summary>Design-time factory so `dotnet ef migrations add` works standalone from this
/// module project before Host wires up real DI. Not used at runtime — Host configures
/// MasterDataDbContext through AddDbContext with the real connection string.
/// Hardcodes the local Postgres superuser rather than calling MasterDataConnectionString.Resolve() —
/// that method now defaults to the unprivileged fynovio_app role (RLS prerequisite), but
/// migrations need superuser/table-owner privileges to create tables and enable RLS in the
/// first place, so this design-time path must stay independent of the runtime default.</summary>
public sealed class MasterDataDbContextFactory : IDesignTimeDbContextFactory<MasterDataDbContext>
{
    private const string DesignTimeConnectionString = "Host=localhost;Database=fynovio_platform;Username=postgres;Password=postgres";

    public MasterDataDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MasterDataDbContext>();
        optionsBuilder
            .UseNpgsql(DesignTimeConnectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", MasterDataDbContext.Schema))
            .UseSnakeCaseNamingConvention();

        return new MasterDataDbContext(optionsBuilder.Options);
    }
}
