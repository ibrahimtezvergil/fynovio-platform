namespace Messaging;

/// <summary>The two roles the messaging runtime connects as. The relay role sees outbox pointers and the ledger across
/// tenants and nothing else (E-1 (a)); the runtime role reads one event's full row under that event's tenant.</summary>
public static class MessagingConnectionStrings
{
    private const string RelayLocalDevDefault = "Host=localhost;Database=fynovio_platform;Username=fynovio_relay;Password=relay";
    private const string RuntimeLocalDevDefault = "Host=localhost;Database=fynovio_platform;Username=fynovio_app;Password=runtime";

    public static string ResolveRelay() =>
        Environment.GetEnvironmentVariable("FYNOVIO_RELAY_CONNECTION_STRING") ?? RelayLocalDevDefault;

    public static string ResolveRuntime() =>
        Environment.GetEnvironmentVariable("FYNOVIO_MESSAGING_RUNTIME_CONNECTION_STRING") ?? RuntimeLocalDevDefault;
}
