using Messaging.Delivery;
using Messaging.Relay;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Messaging;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>The relay and the delivery processor. Consumers are registered separately, as scoped
    /// <see cref="Contracts.IEventConsumer"/> services, by the module that owns them.</summary>
    public static IServiceCollection AddMessagingRuntime(this IServiceCollection services, string relayConnectionString, string runtimeConnectionString)
    {
        services.AddSingleton(new RelayDataSource(NpgsqlDataSource.Create(relayConnectionString)));
        services.AddSingleton(new RuntimeDataSource(NpgsqlDataSource.Create(runtimeConnectionString)));
        services.AddSingleton<ConsumerCatalog>();
        services.AddSingleton<OutboxRelay>();
        services.AddSingleton<OutboxEnvelopeReader>();
        services.AddSingleton<DeliveryProcessor>();
        return services;
    }
}
