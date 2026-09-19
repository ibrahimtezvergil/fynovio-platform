using CRM.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Worker;

/// <summary>Smallest correct at-least-once dispatcher for CRM's outbox (architecture
/// plan §12/§15) — polls, logs, marks processed. Phase 2 has no real downstream
/// consumer yet; a future phase replaces the log line with an actual publish call
/// without touching this polling/marking loop. Scoped narrowly to CRM — Access's and
/// MasterData's identical gap is not addressed here.</summary>
public sealed class OutboxDispatcherService(IServiceScopeFactory scopeFactory, ILogger<OutboxDispatcherService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox dispatch batch failed; will retry next poll.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    public async Task DispatchOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

        var pending = await context.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.Id)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var message in pending)
        {
            logger.LogInformation(
                "Dispatching {EventType} for {AggregateType}/{AggregateId}",
                message.EventType, message.AggregateType, message.AggregateId);
            message.MarkProcessed();
        }

        if (pending.Count > 0)
            await context.SaveChangesAsync(cancellationToken);
    }
}
