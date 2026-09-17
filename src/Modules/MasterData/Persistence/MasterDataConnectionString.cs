namespace MasterData.Persistence;

/// <summary>Single source of truth for the MasterData module's local-dev default
/// connection string, shared by the design-time factory and Host's real DI
/// registration. Defaults to the unprivileged `fynovio_app` role (scripts/create-runtime-role.sql)
/// so RLS is never accidentally bypassed by an unconfigured environment (AGENTS.md Database Rules:
/// "superusers and table owners bypass RLS regardless of policy"). Migrations still run as postgres
/// — see MasterDataDbContextFactory, which is unaffected by this default.</summary>
public static class MasterDataConnectionString
{
    private const string LocalDevDefault = "Host=localhost;Database=fynovio_platform;Username=fynovio_app;Password=runtime";

    public static string Resolve() =>
        Environment.GetEnvironmentVariable("FYNOVIO_MASTERDATA_CONNECTION_STRING") ?? LocalDevDefault;
}
