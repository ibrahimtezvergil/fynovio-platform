using CRM.Domain;
using CRM.Outbox;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Worker;
using Xunit;

namespace CRM.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class OutboxDispatcherServiceTests
{
    private readonly PostgresFixture _fixture;

    public OutboxDispatcherServiceTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task A_single_dispatch_pass_marks_every_pending_message_processed()
    {
        var tenant = TestData.NextTenant();
        await using (var seed = _fixture.CreateAdminContext())
        {
            seed.OutboxMessages.Add(OutboxMessage.Create(
                tenant, nameof(Opportunity), 1, 1, "enterprise.crmsales.opportunity.created.v1",
                "/enterprise/crm-sales", "opportunities/1", Guid.NewGuid(), null, "{}"));
            await seed.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        services.AddSingleton(_ => _fixture.CreateAdminContext());
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<OutboxDispatcherService>>(NullLogger<OutboxDispatcherService>.Instance);
        await using var provider = services.BuildServiceProvider();
        var scopeFactoryStub = new SingleContextScopeFactory(provider);

        var dispatcher = new OutboxDispatcherService(scopeFactoryStub, provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OutboxDispatcherService>>());
        await dispatcher.DispatchOnceAsync(CancellationToken.None);

        await using var verification = _fixture.CreateAdminContext();
        var message = await verification.OutboxMessages.AsNoTracking().SingleAsync(m => m.TenantId == tenant && m.AggregateId == 1);
        Assert.NotNull(message.ProcessedAt);
    }
}

/// <summary>The real IServiceScopeFactory creates a new DI scope (and, if CrmDbContext
/// were scoped, a new instance) per call — this stub always returns the same
/// already-built provider, which is fine for a test that already controls the
/// CrmDbContext instance directly via a singleton registration.</summary>
internal sealed class SingleContextScopeFactory(IServiceProvider provider) : IServiceScopeFactory
{
    public IServiceScope CreateScope() => new NonDisposingScope(provider);

    private sealed class NonDisposingScope(IServiceProvider provider) : IServiceScope
    {
        public IServiceProvider ServiceProvider => provider;
        public void Dispose() { }
    }
}
