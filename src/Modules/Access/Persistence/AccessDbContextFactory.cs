using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Access.Persistence;

/// <summary>Design-time factory so `dotnet ef migrations add` works standalone from this
/// module project before Host wires up real DI. Not used at runtime — Host configures
/// AccessDbContext through AddDbContext with the real connection string and interceptors.
/// Migrations history table lives in the `access` schema — an arbitrary but consistent pick
/// between the two schemas this context owns, since EF Core takes exactly one.
/// Hardcodes the local Postgres superuser rather than calling AccessConnectionString.Resolve() —
/// that method now defaults to the unprivileged fynovio_app role (RLS prerequisite), but
/// migrations need superuser/table-owner privileges to create tables and enable RLS in the
/// first place, so this design-time path must stay independent of the runtime default.</summary>
public sealed class AccessDbContextFactory : IDesignTimeDbContextFactory<AccessDbContext>
{
    private const string DesignTimeConnectionString = "Host=localhost;Database=fynovio_platform;Username=postgres;Password=postgres";

    public AccessDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AccessDbContext>();
        optionsBuilder
            .UseNpgsql(
                DesignTimeConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", AccessDbContext.AccessSchema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new RowVersionInterceptor());

        return new AccessDbContext(optionsBuilder.Options);
    }
}
