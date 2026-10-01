using Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Messaging;

/// <summary>What the relay needs to know about each registered <see cref="IEventConsumer"/>, read once from DI.
/// Consumers are scoped (they hold a module DbContext), so the descriptors are taken in a throwaway scope.</summary>
public sealed class ConsumerCatalog
{
    public IReadOnlyList<ConsumerDescriptor> Consumers { get; }

    public ConsumerCatalog(IServiceScopeFactory scopeFactory)
    {
        using var scope = scopeFactory.CreateScope();
        Consumers = scope.ServiceProvider.GetServices<IEventConsumer>()
            .Select(c => new ConsumerDescriptor(c.Name, c.EventTypes, c.StartPolicy))
            .ToList();

        var duplicate = Consumers.GroupBy(c => c.Name).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"Two event consumers share the name '{duplicate.Key}'.");
        foreach (var consumer in Consumers)
        {
            if (string.IsNullOrWhiteSpace(consumer.Name) || consumer.Name.Length > 100)
                throw new InvalidOperationException("An event consumer name must be 1–100 characters.");
            if (consumer.EventTypes.Count == 0)
                throw new InvalidOperationException($"Event consumer '{consumer.Name}' subscribes to no event type.");
        }
    }
}

public sealed record ConsumerDescriptor(string Name, IReadOnlySet<string> EventTypes, ConsumerStartPolicy StartPolicy);
