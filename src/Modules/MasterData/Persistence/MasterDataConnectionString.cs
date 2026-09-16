namespace MasterData.Persistence;

/// <summary>Single source of truth for the MasterData module's local-dev default
/// connection string, shared by the design-time factory and Host's real DI
/// registration. Production overrides via ConnectionStrings__MasterData.</summary>
public static class MasterDataConnectionString
{
    private const string LocalDevDefault = "Host=localhost;Database=fynovio_platform;Username=postgres;Password=postgres";

    public static string Resolve() =>
        Environment.GetEnvironmentVariable("FYNOVIO_MASTERDATA_CONNECTION_STRING") ?? LocalDevDefault;
}
