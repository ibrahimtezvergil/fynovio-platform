using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Access.Persistence;

/// <summary>Design-time factory so `dotnet ef migrations add` works standalone from this
/// module project before Host wires up real DI. Not used at runtime — Host configures
/// AccessDbContext through AddDbContext with the real connection string and interceptors.
/// Migrations history table lives in the `access` schema — an arbitrary but consistent pick
/// between the two schemas this context owns, since EF Core takes exactly one.</summary>
public sealed class AccessDbContextFactory : IDesignTimeDbContextFactory<AccessDbContext>
{
    public AccessDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AccessDbContext>();
        optionsBuilder
            .UseNpgsql(
                AccessConnectionString.Resolve(),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", AccessDbContext.AccessSchema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new RowVersionInterceptor());

        return new AccessDbContext(optionsBuilder.Options);
    }
}
