namespace CRM.Persistence;

/// <summary>Single source of truth for the CRM module's local-dev default connection
/// string, shared by the design-time factory and Host's real DI registration so the
/// fallback lives in exactly one place. Production overrides this via configuration
/// (e.g. the ConnectionStrings__Crm environment variable) rather than this default.</summary>
public static class CrmConnectionString
{
    private const string LocalDevDefault = "Host=localhost;Database=fynovio_platform;Username=postgres;Password=postgres";

    public static string Resolve() =>
        Environment.GetEnvironmentVariable("FYNOVIO_CRM_CONNECTION_STRING") ?? LocalDevDefault;
}
