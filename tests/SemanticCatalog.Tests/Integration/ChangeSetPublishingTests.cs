using Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SemanticCatalog.Application;
using SemanticCatalog.Domain;
using SemanticCatalog.Persistence;
using Xunit;

namespace SemanticCatalog.Tests.Integration;

/// <summary>The publish engine on real PostgreSQL: the per-tenant revision lock, the stale-base rule, atomicity of a
/// multi-item set, and the facts a publish writes (outbox, evidence) — with RLS proven as the runtime role.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class ChangeSetPublishingTests(PostgresFixture fixture)
{
    private static readonly PrincipalRef Administrator = new("https://identity.test", "crm-settings-admin");

    private static ChangeSetItem Create(string key, string label = "Label", FieldType type = FieldType.Text, FieldConfig? config = null) =>
        ChangeSetItem.ForField(ChangeOperation.Create, null, new FieldChangeContent("crm", "opportunity", key, label, type, false, config, 0, 0));

    /// <summary>Drives a set through the human approval path and publishes it in one transaction, like the settings handler.</summary>
    private async Task<(PublishOutcome Outcome, SemanticCatalogDbContext Context)> PublishAsync(
        TenantId tenant, Func<ChangeSetPublisher, Task<ChangeSet>> draft, SemanticCatalogDbContext? existing = null)
    {
        var context = existing ?? fixture.CreateAdminContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenant);
        var publisher = new ChangeSetPublisher(context);
        var set = await draft(publisher);
        set.Validate();
        set.SubmitForApproval();
        set.Approve();
        var outcome = await publisher.PublishAsync(set, Administrator, Guid.NewGuid());
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return (outcome, context);
    }

    [Fact]
    public async Task A_published_set_applies_every_item_bumps_the_revision_and_is_active()
    {
        var tenant = TestTenants.Next();

        var (outcome, context) = await PublishAsync(tenant, p => p.DraftAsync(tenant, Administrator, [Create("alpha"), Create("beta")]));
        await using (context)
        {
            Assert.True(outcome.Published);
            Assert.Equal(ChangeSetStatus.Active, outcome.ChangeSet.Status);
            Assert.Equal(1, outcome.ChangeSet.PublishedRevision);
            Assert.Equal(["alpha", "beta"], outcome.Fields.Select(d => d.Key));
        }

        await using var verify = fixture.CreateAdminContext();
        Assert.Equal(["alpha", "beta"], (await verify.FieldDefinitions.Where(d => d.TenantId == tenant).OrderBy(d => d.Key).ToListAsync()).Select(d => d.Key));
        Assert.Equal(1, (await verify.CatalogRevisions.SingleAsync(r => r.TenantId == tenant)).Revision);
        var stored = await verify.ChangeSets.Include(c => c.Items).SingleAsync(c => c.TenantId == tenant);
        Assert.Equal(ChangeSetStatus.Active, stored.Status);
        Assert.Equal(2, stored.Items.Count);
        Assert.Equal(outcome.ChangeSet.ContentHash, stored.ContentHash);
    }

    [Fact]
    public async Task A_publish_writes_the_set_event_one_field_event_per_item_and_evidence_with_the_self_approval()
    {
        var tenant = TestTenants.Next();
        var (outcome, context) = await PublishAsync(tenant, p => p.DraftAsync(tenant, Administrator, [Create("alpha"), Create("beta")]));
        await context.DisposeAsync();

        await using var verify = fixture.CreateAdminContext();
        var events = await verify.OutboxMessages.Where(m => m.TenantId == tenant).ToListAsync();
        var published = Assert.Single(events, e => e.EventType == ChangeSetPublisher.PublishedEventType);
        Assert.Equal(outcome.ChangeSet.Id, published.AggregateId);
        using (var payload = System.Text.Json.JsonDocument.Parse(published.Payload))
        {
            Assert.Equal(2, payload.RootElement.GetProperty("itemCount").GetInt32());
            Assert.Equal(["alpha", "beta"], payload.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()));
        }
        Assert.DoesNotContain("Label", published.Payload);   // keys only: no definition content leaves the catalog (OD-7)
        Assert.Equal(2, events.Count(e => e.EventType == ChangeSetPublisher.FieldChangedEventType));

        var evidence = await verify.EvidenceRecords.Where(e => e.TenantId == tenant && e.AggregateType == "ChangeSet").SingleAsync();
        Assert.Equal("ChangeSet.Published", evidence.Action);
        using (var detail = System.Text.Json.JsonDocument.Parse(evidence.Detail))
            Assert.Equal("self", detail.RootElement.GetProperty("approval").GetString());
        Assert.Contains("Draft->Validated", evidence.Detail);
        Assert.Contains("Published->Active", evidence.Detail);
        Assert.Equal(2, await verify.EvidenceRecords.CountAsync(e => e.TenantId == tenant && e.AggregateType == "TenantFieldDefinition"));
    }

    [Fact]
    public async Task A_set_authored_against_an_older_revision_is_superseded_and_applies_nothing()
    {
        var tenant = TestTenants.Next();
        // Two drafts authored against the same revision (0).
        ChangeSet stale;
        await using (var draftContext = fixture.CreateAdminContext())
        {
            await using var transaction = await draftContext.Database.BeginTransactionAsync();
            await draftContext.SetTenantContextAsync(tenant);
            stale = await new ChangeSetPublisher(draftContext).DraftAsync(tenant, Administrator, [Create("stale_field")]);
        }

        var (winner, winnerContext) = await PublishAsync(tenant, p => p.DraftAsync(tenant, Administrator, [Create("winner_field")]));
        await winnerContext.DisposeAsync();
        Assert.True(winner.Published);

        var (loser, loserContext) = await PublishAsync(tenant, _ => Task.FromResult(stale));
        await loserContext.DisposeAsync();

        Assert.False(loser.Published);
        Assert.Equal(ChangeSetStatus.Superseded, loser.ChangeSet.Status);
        Assert.Null(loser.ChangeSet.PublishedRevision);
        await using var verify = fixture.CreateAdminContext();
        Assert.Equal(["winner_field"], (await verify.FieldDefinitions.Where(d => d.TenantId == tenant).ToListAsync()).Select(d => d.Key));
        Assert.Equal(1, (await verify.CatalogRevisions.SingleAsync(r => r.TenantId == tenant)).Revision);
        Assert.Equal(ChangeSetStatus.Superseded, (await verify.ChangeSets.SingleAsync(c => c.Id == stale.Id)).Status);
        Assert.Equal(1, await verify.OutboxMessages.CountAsync(m => m.TenantId == tenant && m.EventType == ChangeSetPublisher.PublishedEventType));
    }

    [Fact]
    public async Task A_failing_item_rolls_the_whole_set_back()
    {
        var tenant = TestTenants.Next();
        await using (var seed = fixture.CreateAdminContext())
        {
            seed.FieldDefinitions.Add(CatalogFieldDefinition.Create(tenant, "crm", "opportunity", "taken", "Taken", FieldType.Text));
            await seed.SaveChangesAsync();
        }

        await using var context = fixture.CreateAdminContext();
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
            await PublishAsync(tenant, p => p.DraftAsync(tenant, Administrator, [Create("fresh"), Create("taken")]), context));

        await using var verify = fixture.CreateAdminContext();
        Assert.Equal(["taken"], (await verify.FieldDefinitions.Where(d => d.TenantId == tenant).ToListAsync()).Select(d => d.Key));
        Assert.False(await verify.ChangeSets.AnyAsync(c => c.TenantId == tenant));
        Assert.False(await verify.CatalogRevisions.AnyAsync(r => r.TenantId == tenant));
        Assert.False(await verify.OutboxMessages.AnyAsync(m => m.TenantId == tenant));
    }

    [Fact]
    public async Task Concurrent_publishes_for_one_tenant_serialize_into_distinct_revisions()
    {
        var tenant = TestTenants.Next();

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(async i =>
        {
            var handler = new ManageFieldDefinitionHandler(fixture.CreateAdminContext(), StubAuthorizer.AlwaysAllow);
            return await handler.HandleAsync(new ManageFieldDefinitionCommand(
                tenant, Administrator, ChangeOperation.Create, null, 0, "crm", "opportunity", $"field_{i}", $"Field {i}", FieldType.Text, false, null, 0, $"concurrent-{tenant.Value}-{i}", Guid.NewGuid()));
        }));

        Assert.Equal(6, results.Select(r => r.ChangeSetId).Distinct().Count());
        await using var verify = fixture.CreateAdminContext();
        Assert.Equal(6, (await verify.CatalogRevisions.SingleAsync(r => r.TenantId == tenant)).Revision);
        Assert.Equal([1L, 2, 3, 4, 5, 6], (await verify.ChangeSets.Where(c => c.TenantId == tenant).Select(c => c.PublishedRevision!.Value).ToListAsync()).Order());
        Assert.All(await verify.ChangeSets.Where(c => c.TenantId == tenant).ToListAsync(), c => Assert.Equal(ChangeSetStatus.Active, c.Status));
    }

    [Fact]
    public async Task A_publish_requires_an_approved_set_and_a_transaction()
    {
        var tenant = TestTenants.Next();
        await using var context = fixture.CreateAdminContext();
        var publisher = new ChangeSetPublisher(context);

        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenant);
        var draft = await publisher.DraftAsync(tenant, Administrator, [Create("alpha")]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.PublishAsync(draft, Administrator, Guid.NewGuid()));
        await transaction.RollbackAsync();

        draft.Validate(); draft.SubmitForApproval(); draft.Approve();
        await using var other = fixture.CreateAdminContext();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ChangeSetPublisher(other).PublishAsync(draft, Administrator, Guid.NewGuid()));
    }

    [Fact]
    public async Task The_runtime_role_sees_only_its_own_tenants_change_sets_and_revision()
    {
        var (tenantA, tenantB) = (TestTenants.Next(), TestTenants.Next());
        foreach (var tenant in new[] { tenantA, tenantB })
        {
            var (_, context) = await PublishAsync(tenant, p => p.DraftAsync(tenant, Administrator, [Create("seeded")]));
            await context.DisposeAsync();
        }

        await using var runtime = PostgresFixture.CreateContext(await fixture.RuntimeConnectionStringAsync());
        Assert.Empty(await runtime.ChangeSets.AsNoTracking().ToListAsync());
        Assert.Empty(await runtime.CatalogRevisions.AsNoTracking().ToListAsync());

        await using var transaction = await runtime.Database.BeginTransactionAsync();
        await runtime.SetTenantContextAsync(tenantB);
        Assert.All(await runtime.ChangeSets.AsNoTracking().ToListAsync(), c => Assert.Equal(tenantB, c.TenantId));
        Assert.Equal(tenantB, Assert.Single(await runtime.CatalogRevisions.AsNoTracking().ToListAsync()).TenantId);
        Assert.Equal(tenantB, Assert.Single(await runtime.ChangeSetItems.AsNoTracking().ToListAsync()).TenantId);
    }

    [Theory]
    [InlineData("UPDATE semantic.change_sets SET status = 'Gone', published_revision = NULL", "ck_change_sets_status")]
    [InlineData("UPDATE semantic.change_sets SET published_revision = NULL", "ck_change_sets_published_revision")]
    [InlineData("UPDATE semantic.change_sets SET content_hash = repeat('G', 64)", "ck_change_sets_content_hash")]
    [InlineData("UPDATE semantic.change_sets SET source = 'Ai'", "ck_change_sets_source")]
    [InlineData("UPDATE semantic.change_set_items SET target_id = 7", "ck_change_set_items_target")]
    public async Task The_database_enforces_the_change_set_shape(string sql, string constraint)
    {
        var tenant = TestTenants.Next();
        var (_, context) = await PublishAsync(tenant, p => p.DraftAsync(tenant, Administrator, [Create("shaped")]));
        await context.DisposeAsync();

        await using var connection = new NpgsqlConnection(fixture.AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"{sql} WHERE tenant_id = {tenant.Value}", connection);
        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(constraint, exception.ConstraintName);
    }
}
