using Contracts;
using CRM.Application;
using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Integration;

/// <summary>FF07 + FF08 (doc 12): state, outbox, evidence ve idempotency kaydı tek
/// transaction'da commit olur; aynı anahtarla tekrar gelen komut tek iş etkisi yaratır.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class CompleteOpportunityHandlerTests
{
    private readonly PostgresFixture _fixture;

    public CompleteOpportunityHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Completing_writes_state_outbox_and_evidence_together()
    {
        var (tenant, opportunityId) = await SeedOpenOpportunityAsync();
        var command = NewCommand(tenant, opportunityId, "key-1");

        await using (var context = _fixture.CreateAdminContext())
        {
            var result = await new CompleteOpportunityHandler(context).HandleAsync(command);
            Assert.False(result.Replayed);
            Assert.Equal(100.00m, result.TotalAmount);
        }

        await using var verification = _fixture.CreateAdminContext();
        var opportunity = await verification.Opportunities.AsNoTracking()
            .SingleAsync(o => o.Id == opportunityId);
        var outbox = await verification.OutboxMessages.AsNoTracking()
            .Where(m => m.AggregateId == opportunityId).ToListAsync();
        var evidence = await verification.EvidenceRecords.AsNoTracking()
            .Where(e => e.AggregateId == opportunityId).ToListAsync();

        Assert.Equal(OpportunityStatus.Won, opportunity.Status);
        var message = Assert.Single(outbox);
        var record = Assert.Single(evidence);
        Assert.Equal("enterprise.crmsales.opportunity.completed.v1", message.EventType);
        Assert.Equal("/enterprise/crm-sales", message.Source);
        Assert.Equal($"opportunities/{opportunityId}", message.Subject);
        Assert.Equal(opportunity.RowVersion, message.AggregateVersion);
        Assert.Equal(opportunity.RowVersion, record.AggregateVersion);
        Assert.Equal(command.CorrelationId, message.CorrelationId);
        Assert.Equal(command.CorrelationId, record.CorrelationId);
        Assert.Equal("Opportunity.Win", record.Action);
        Assert.Equal(TestData.Seller.Subject, record.PrincipalSubject);
    }

    [Fact]
    public async Task Retrying_with_the_same_key_replays_the_stored_response()
    {
        var (tenant, opportunityId) = await SeedOpenOpportunityAsync();
        var command = NewCommand(tenant, opportunityId, "key-2");

        await using (var first = _fixture.CreateAdminContext())
        {
            await new CompleteOpportunityHandler(first).HandleAsync(command);
        }

        await using (var second = _fixture.CreateAdminContext())
        {
            var replay = await new CompleteOpportunityHandler(second).HandleAsync(command);
            Assert.True(replay.Replayed);
            Assert.Equal(opportunityId, replay.OpportunityId);
            Assert.Equal(100.00m, replay.TotalAmount);
        }

        await using var verification = _fixture.CreateAdminContext();
        Assert.Single(await verification.OutboxMessages.AsNoTracking()
            .Where(m => m.AggregateId == opportunityId).ToListAsync());
        Assert.Single(await verification.EvidenceRecords.AsNoTracking()
            .Where(e => e.AggregateId == opportunityId).ToListAsync());
    }

    [Fact]
    public async Task Reusing_a_key_for_a_different_request_is_rejected()
    {
        var (tenant, firstId) = await SeedOpenOpportunityAsync(tenant: null);
        var (_, secondId) = await SeedOpenOpportunityAsync(tenant);

        await using (var first = _fixture.CreateAdminContext())
        {
            await new CompleteOpportunityHandler(first).HandleAsync(NewCommand(tenant, firstId, "key-shared"));
        }

        await using var second = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() =>
            new CompleteOpportunityHandler(second).HandleAsync(NewCommand(tenant, secondId, "key-shared")));

        await using var verification = _fixture.CreateAdminContext();
        var untouched = await verification.Opportunities.AsNoTracking().SingleAsync(o => o.Id == secondId);
        Assert.Equal(OpportunityStatus.Open, untouched.Status);
    }

    [Fact]
    public async Task A_rejected_command_leaves_no_outbox_evidence_or_idempotency_row()
    {
        var tenant = TestData.NextTenant();
        long opportunityId;

        await using (var seed = _fixture.CreateAdminContext())
        {
            var party = Party.Create(tenant, "Acme", PartyCreationSource.Manual);
            seed.Parties.Add(party);
            await seed.SaveChangesAsync();

            // Still waiting: Complete() will reject it.
            var opportunity = Opportunity.Create(tenant, party.Id, TestData.Seller, "TRY", 100m);
            opportunity.AddLine(TestData.ProductRef(tenant), quantity: 1, unitPrice: 100m);
            seed.Opportunities.Add(opportunity);
            await seed.SaveChangesAsync();
            opportunityId = opportunity.Id;
        }

        await using (var context = _fixture.CreateAdminContext())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new CompleteOpportunityHandler(context).HandleAsync(NewCommand(tenant, opportunityId, "key-3")));
        }

        await using var verification = _fixture.CreateAdminContext();
        Assert.Empty(await verification.OutboxMessages.AsNoTracking()
            .Where(m => m.AggregateId == opportunityId).ToListAsync());
        Assert.Empty(await verification.EvidenceRecords.AsNoTracking()
            .Where(e => e.AggregateId == opportunityId).ToListAsync());
        Assert.Empty(await verification.IdempotencyRecords.AsNoTracking()
            .Where(r => r.TenantId == tenant && r.IdempotencyKey == "key-3").ToListAsync());
    }

    [Fact]
    public async Task Completing_works_under_the_runtime_role()
    {
        var (tenant, opportunityId) = await SeedOpenOpportunityAsync();

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        var result = await new CompleteOpportunityHandler(context).HandleAsync(NewCommand(tenant, opportunityId, "key-4"));

        Assert.False(result.Replayed);
        Assert.Equal(100.00m, result.TotalAmount);
    }

    [Fact]
    public async Task A_command_for_another_tenants_opportunity_is_not_found_under_the_runtime_role()
    {
        var (_, opportunityId) = await SeedOpenOpportunityAsync();
        var otherTenant = TestData.NextTenant();

        await using (var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync()))
        {
            await Assert.ThrowsAsync<OpportunityNotFoundException>(() =>
                new CompleteOpportunityHandler(context).HandleAsync(NewCommand(otherTenant, opportunityId, "key-5")));
        }

        await using var verification = _fixture.CreateAdminContext();
        var untouched = await verification.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunityId);
        Assert.Equal(OpportunityStatus.Open, untouched.Status);
    }

    private static CompleteOpportunityCommand NewCommand(TenantId tenant, long opportunityId, string key) =>
        new(tenant, opportunityId, TestData.Seller, key, Guid.NewGuid());

    private async Task<(TenantId TenantId, long OpportunityId)> SeedOpenOpportunityAsync(TenantId? tenant = null)
    {
        var tenantId = tenant ?? TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();

        var party = Party.Create(tenantId, "Acme", PartyCreationSource.Manual);
        seed.Parties.Add(party);
        await seed.SaveChangesAsync();

        var opportunity = Opportunity.Create(tenantId, party.Id, TestData.Seller, "TRY", 100m);
        opportunity.AddLine(TestData.ProductRef(tenantId), quantity: 1, unitPrice: 100m);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7));
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        return (tenantId, opportunity.Id);
    }
}
