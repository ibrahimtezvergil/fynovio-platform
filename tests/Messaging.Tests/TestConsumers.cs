using System.Collections.Concurrent;
using Contracts;
using Messaging.Delivery;
using Messaging.Relay;
using Microsoft.Extensions.DependencyInjection;

namespace Messaging.Tests;

/// <summary>What a test consumer saw, and whether it should fail. One per test, shared by every scope.</summary>
public sealed class ConsumerProbe
{
    public ConcurrentQueue<EventEnvelope> Received { get; } = new();
    public Func<EventEnvelope, Exception?> Fail { get; set; } = _ => null;
}

public sealed class ProbeConsumer(string name, IReadOnlySet<string> eventTypes, ConsumerStartPolicy startPolicy, ConsumerProbe probe) : IEventConsumer
{
    public string Name => name;
    public IReadOnlySet<string> EventTypes => eventTypes;
    public ConsumerStartPolicy StartPolicy => startPolicy;

    public Task HandleAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (probe.Fail(envelope) is { } exception)
            throw exception;
        probe.Received.Enqueue(envelope);
        return Task.CompletedTask;
    }
}

/// <summary>The messaging runtime as Worker wires it — relay role and runtime role, never the migration role — with
/// one probe consumer under a name unique to the test.</summary>
public sealed class MessagingHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public MessagingHarness(MessagingFixture fixture, IReadOnlySet<string> eventTypes, ConsumerStartPolicy startPolicy = ConsumerStartPolicy.FromBeginning)
    {
        ConsumerName = $"test-{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Probe);
        services.AddScoped<IEventConsumer>(sp => new ProbeConsumer(ConsumerName, eventTypes, startPolicy, sp.GetRequiredService<ConsumerProbe>()));
        services.AddMessagingRuntime(fixture.RelayConnectionString, fixture.RuntimeConnectionString);
        _provider = services.BuildServiceProvider();
    }

    public string ConsumerName { get; }
    public ConsumerProbe Probe { get; } = new();
    public OutboxRelay Relay => _provider.GetRequiredService<OutboxRelay>();
    public DeliveryProcessor Processor => _provider.GetRequiredService<DeliveryProcessor>();

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}

public static class TestTenants
{
    private static long _next = Random.Shared.NextInt64(1_000_000, 9_000_000);

    public static long Next() => Interlocked.Increment(ref _next);
}
