namespace TenantLifecycle.Persistence;

public static class TenantLifecycleConnectionString
{
    private const string LocalDevDefault = "Host=localhost;Database=fynovio_platform;Username=fynovio_app;Password=runtime";

    public static string Resolve() =>
        Environment.GetEnvironmentVariable("FYNOVIO_TENANT_LIFECYCLE_CONNECTION_STRING") ?? LocalDevDefault;
}
