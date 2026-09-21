namespace Collaboration.Persistence;

public static class CollaborationConnectionString
{
    private const string LocalDevDefault = "Host=localhost;Database=fynovio_platform;Username=fynovio_app;Password=runtime";

    public static string Resolve() =>
        Environment.GetEnvironmentVariable("FYNOVIO_COLLABORATION_CONNECTION_STRING") ?? LocalDevDefault;
}
