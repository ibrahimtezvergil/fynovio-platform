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
        var optionsBuilder = new DbContextOptionsBuilder<CrmDbContext>();
        optionsBuilder
            .UseNpgsql(CrmConnectionString.Resolve(), npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CrmDbContext.Schema))
            .UseSnakeCaseNamingConvention();

        return new CrmDbContext(optionsBuilder.Options);
    }
}
