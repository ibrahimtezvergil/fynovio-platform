namespace Access.Persistence;

/// <summary>Single source of truth for the Access module's local-dev default connection
/// string, mirroring CRM.Persistence.CrmConnectionString. Same physical Postgres instance as
/// CRM by default (separate `identity`/`access` schemas, not a separate database) —
/// production overrides this via configuration (e.g. the ConnectionStrings__Access
/// environment variable) rather than this default.</summary>
public static class AccessConnectionString
{
    private const string LocalDevDefault = "Host=localhost;Database=fynovio_platform;Username=postgres;Password=postgres";

    public static string Resolve() =>
        Environment.GetEnvironmentVariable("FYNOVIO_ACCESS_CONNECTION_STRING") ?? LocalDevDefault;
}
