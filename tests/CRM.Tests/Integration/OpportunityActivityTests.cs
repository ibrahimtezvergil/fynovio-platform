using System.Text.Json;
using Contracts;
using CRM.Activity;
using CRM.Application;
using CRM.Customization;
using CRM.Domain;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Integration;

/// <summary>The first real event consumer (adr-event-consumption.md, E-2 (a)) and the timeline it feeds. The consumer
/// runs as the runtime role — RLS applies — exactly as the Worker runs it.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class OpportunityActivityTests(PostgresFixture fixture)
{
    private static EventEnvelope Envelope(TenantId tenant, long opportunityId, string kind, long version, object payload, DateTimeOffset? occurredAt = null) =>
        new(Guid.NewGuid(), $"enterprise.crmsales.opportunity.{kind}.v1", "/enterprise/crm-sales", $"opportunities/{opportunityId}", tenant,
            nameof(Opportunity), opportunityId, version, Guid.NewGuid(), null, occurredAt ?? DateTimeOffset.UtcNow, JsonSerializer.Serialize(payload));

    private async Task ConsumeAsync(EventEnvelope envelope)
    {
        await using var context = PostgresFixture.CreateContext(await fixture.RuntimeConnectionStringAsync());
        await new OpportunityActivityConsumer(context).HandleAsync(envelope, CancellationToken.None);
    }

    private async Task<(TenantId Tenant, long OpportunityId)> SeedOpportunityAsync()
    {
        var tenant = TestData.NextTenant();
        await using var seed = fixture.CreateAdminContext();
        await using var masterData = fixture.CreateMasterDataContext();
        var party = await TestData.CreatePartyAsync(masterData, tenant, "Acme");
        var opportunity = Opportunity.Create(tenant, party, TestData.Seller, "TRY", 1000m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();
        return (tenant, opportunity.Id);
    }

    [Fact]
    public void Subscribes_to_every_opportunity_fact_crm_publishes()
    {
        var consumer = new OpportunityActivityConsumer(fixture.CreateAdminContext());

        Assert.Equal(ConsumerStartPolicy.FromBeginning, consumer.StartPolicy);
        Assert.Equal(
            new[] { "archived", "created", "custom_fields_changed", "lost", "moved_pipeline", "opened", "reassigned", "restored", "stage_changed", "won" }
                .Select(kind => $"enterprise.crmsales.opportunity.{kind}.v1").Order(),
            consumer.EventTypes.Order());
    }

    [Fact]
    public async Task A_redelivered_event_is_applied_once()
    {
        var (tenant, opportunityId) = await SeedOpportunityAsync();
        var envelope = Envelope(tenant, opportunityId, "won", 3, new { OpportunityId = opportunityId, TotalAmount = 1200m, Currency = "TRY" });

        await ConsumeAsync(envelope);
        await ConsumeAsync(envelope);

        await using var admin = fixture.CreateAdminContext();
        Assert.Equal(1, await admin.OpportunityActivity.CountAsync(e => e.EventId == envelope.EventId));
        Assert.Equal(1, await admin.ConsumedEvents.CountAsync(e => e.EventId == envelope.EventId && e.Consumer == OpportunityActivityConsumer.ConsumerName));
        var entry = await admin.OpportunityActivity.SingleAsync(e => e.EventId == envelope.EventId);
        Assert.Equal(("won", opportunityId, tenant, 3L), (entry.Kind, entry.OpportunityId, entry.TenantId, entry.AggregateVersion));
    }

    [Fact]
    public async Task The_projection_is_tenant_isolated_for_the_runtime_role()
    {
        var (tenant, opportunityId) = await SeedOpportunityAsync();
        var envelope = Envelope(tenant, opportunityId, "created", 1, new { OpportunityId = opportunityId });
        await ConsumeAsync(envelope);

        await using var other = PostgresFixture.CreateContext(await fixture.RuntimeConnectionStringAsync());
        await using var transaction = await other.Database.BeginTransactionAsync();
        await other.SetTenantContextAsync(TestData.NextTenant());
        Assert.False(await other.OpportunityActivity.AnyAsync(e => e.EventId == envelope.EventId));
        Assert.False(await other.ConsumedEvents.AnyAsync(e => e.EventId == envelope.EventId));
    }

    [Fact]
    public async Task The_timeline_is_newest_first_and_labelled_at_read_time()
    {
        var (tenant, opportunityId) = await SeedOpportunityAsync();
        long fromStage, toStage;
        await using (var admin = fixture.CreateAdminContext())
        {
            (_, fromStage) = await TestData.CreatePublishedPipelineWithEntryStageAsync(admin, tenant);
            (_, toStage) = await TestData.CreatePublishedPipelineWithEntryStageAsync(admin, tenant);
            await admin.Database.ExecuteSqlInterpolatedAsync($"UPDATE crm.pipeline_stages SET name = 'Proposal' WHERE id = {toStage}");
            admin.TenantFieldDefinitions.Add(TenantFieldDefinition.Create(tenant, TenantFieldAggregateType.Opportunity, "region", "Region", TenantFieldValueType.Text));
            await admin.SaveChangesAsync();
        }
        var newOwner = new PrincipalRef("https://idp.local", "seller-2");
        var start = DateTimeOffset.UtcNow.AddMinutes(-10);
        await ConsumeAsync(Envelope(tenant, opportunityId, "created", 1, new { OpportunityId = opportunityId, Currency = "TRY", EstimatedAmount = 1000m }, start));
        await ConsumeAsync(Envelope(tenant, opportunityId, "stage_changed", 2, new { OpportunityId = opportunityId, FromStageId = fromStage, ToStageId = toStage }, start.AddMinutes(1)));
        await ConsumeAsync(Envelope(tenant, opportunityId, "reassigned", 3, new { OpportunityId = opportunityId, PreviousPrincipal = TestData.Seller.ToString(), NewPrincipal = newOwner.ToString() }, start.AddMinutes(2)));
        await ConsumeAsync(Envelope(tenant, opportunityId, "custom_fields_changed", 4, new { OpportunityId = opportunityId, ChangedKeys = new[] { "region", "gone" } }, start.AddMinutes(3)));
        await ConsumeAsync(Envelope(tenant, opportunityId, "lost", 5, new { OpportunityId = opportunityId, LostReason = "Price" }, start.AddMinutes(4)));
        // Another opportunity's fact in the same tenant stays off this timeline.
        await ConsumeAsync(Envelope(tenant, opportunityId + 1, "created", 1, new { OpportunityId = opportunityId + 1 }));

        await using var context = fixture.CreateAdminContext();
        var timeline = await new GetOpportunityActivityHandler(context, StubAuthorizer.AlwaysAllow, StubPrincipalDirectory.Permitting(newOwner))
            .HandleAsync(new GetOpportunityActivityQuery(tenant, opportunityId, TestData.Seller, Guid.NewGuid()));

        Assert.NotNull(timeline);
        Assert.Equal(["lost", "custom_fields_changed", "reassigned", "stage_changed", "created"], timeline!.Select(e => e.Kind));
        Assert.Equal("Price", timeline[0].LostReason);
        Assert.Equal(["Region", "gone"], timeline[1].ChangedFields!);
        Assert.Equal("seller-2", timeline[2].PrincipalName);
        Assert.Equal(("Open", "Proposal"), (timeline[3].FromStageName, timeline[3].StageName));
        Assert.Equal((1000m, "TRY"), (timeline[4].Amount, timeline[4].Currency));
    }

    [Fact]
    public async Task A_denied_or_missing_opportunity_has_no_timeline()
    {
        var (tenant, opportunityId) = await SeedOpportunityAsync();
        await ConsumeAsync(Envelope(tenant, opportunityId, "created", 1, new { OpportunityId = opportunityId }));
        await using var context = fixture.CreateAdminContext();

        Assert.Null(await new GetOpportunityActivityHandler(context, StubAuthorizer.RecordDenied)
            .HandleAsync(new GetOpportunityActivityQuery(tenant, opportunityId, TestData.Seller, Guid.NewGuid())));
        Assert.Null(await new GetOpportunityActivityHandler(context, StubAuthorizer.AlwaysAllow)
            .HandleAsync(new GetOpportunityActivityQuery(tenant, 999_999_999, TestData.Seller, Guid.NewGuid())));
        Assert.Null(await new GetOpportunityActivityHandler(context, StubAuthorizer.AlwaysAllow)
            .HandleAsync(new GetOpportunityActivityQuery(TestData.NextTenant(), opportunityId, TestData.Seller, Guid.NewGuid())));
    }

    [Fact]
    public async Task Asks_the_pdp_the_same_question_as_reading_the_opportunity()
    {
        var (tenant, opportunityId) = await SeedOpportunityAsync();
        var recorder = new RecordingAuthorizer();
        await using var context = fixture.CreateAdminContext();

        await new GetOpportunityActivityHandler(context, recorder).HandleAsync(new GetOpportunityActivityQuery(tenant, opportunityId, TestData.Seller, Guid.NewGuid()));
        await new GetOpportunityHandler(context, recorder).HandleAsync(new GetOpportunityQuery(tenant, opportunityId, TestData.Seller, Guid.NewGuid()));

        Assert.Equal(2, recorder.Requests.Count);
        Assert.Equal(recorder.Requests[1].Action, recorder.Requests[0].Action);
        Assert.Equal(recorder.Requests[1].Resource, recorder.Requests[0].Resource);
    }

    private sealed class RecordingAuthorizer : IAuthorizer
    {
        public List<AuthorizationRequest> Requests { get; } = [];

        public Task<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return StubAuthorizer.AlwaysAllow.AuthorizeAsync(request, cancellationToken);
        }
    }
}
