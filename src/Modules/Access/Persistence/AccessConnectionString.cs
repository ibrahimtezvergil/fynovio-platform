namespace Access.Persistence;

/// <summary>Single source of truth for the Access module's local-dev default connection
/// string, mirroring CRM.Persistence.CrmConnectionString. Same physical Postgres instance as
/// CRM by default (separate `identity`/`access` schemas, not a separate database). Defaults
/// to the unprivileged `fynovio_app` role (scripts/create-runtime-role.sql) so RLS is never
/// accidentally bypassed by an unconfigured environment (AGENTS.md Database Rules: "superusers
/// and table owners bypass RLS regardless of policy"). Migrations still run as postgres — see
/// AccessDbContextFactory, which is unaffected by this default.</summary>
public static class AccessConnectionString
{
    private const string LocalDevDefault = "Host=localhost;Database=fynovio_platform;Username=fynovio_app;Password=runtime";

    public static string Resolve() =>
        Environment.GetEnvironmentVariable("FYNOVIO_ACCESS_CONNECTION_STRING") ?? LocalDevDefault;
}
