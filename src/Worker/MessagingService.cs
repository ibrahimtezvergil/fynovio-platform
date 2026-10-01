using Messaging.Delivery;
using Messaging.Relay;

namespace Worker;

/// <summary>Polls the outbox relay and the delivery processor (adr-event-consumption.md). Replaces the CRM-only
/// dispatcher, which ran as the runtime role without a tenant context and so never saw a row.</summary>
public sealed class MessagingService(OutboxRelay relay, DeliveryProcessor processor, ILogger<MessagingService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await relay.RelayOnceAsync(stoppingToken);
                await processor.ProcessOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError("Messaging pass failed ({Error}); retrying next poll.", DeliveryProcessor.Describe(exception));
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
