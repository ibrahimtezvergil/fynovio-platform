namespace SemanticCatalog.Persistence;

/// <summary>Local-dev default connection string for the catalog, shared by Host's DI registration and the tests.
/// Defaults to the unprivileged `fynovio_app` role so RLS is never bypassed by an unconfigured environment (same rule
/// as the other modules); migrations run as `postgres` through <see cref="SemanticCatalogDbContextFactory"/>.</summary>
public static class SemanticCatalogConnectionString
{
    private const string LocalDevDefault = "Host=localhost;Database=fynovio_platform;Username=fynovio_app;Password=runtime";

    public static string Resolve() =>
        Environment.GetEnvironmentVariable("FYNOVIO_SEMANTICCATALOG_CONNECTION_STRING") ?? LocalDevDefault;
}
