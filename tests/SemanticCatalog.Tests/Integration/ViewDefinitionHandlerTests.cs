using Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SemanticCatalog.Application;
using SemanticCatalog.Domain;
using SemanticCatalog.Persistence;
using Xunit;

namespace SemanticCatalog.Tests.Integration;

/// <summary>Shared table views and their dependency edges on real PostgreSQL (adr-semantic-catalog-changeset.md S-7): edges are computed
/// in the publish transaction from the view's content, never edited by hand, and keyed by field *id*.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class ViewDefinitionHandlerTests(PostgresFixture fixture)
{
    private static readonly PrincipalRef Administrator = new("https://identity.test", "crm-settings-admin");

    private async Task<long> DefineFieldAsync(TenantId tenant, string key, bool deprecate = false)
    {
        await using var context = fixture.CreateAdminContext();
        var created = await new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(new ManageFieldDefinitionCommand(
            tenant, Administrator, ChangeOperation.Create, null, 0, "crm", "opportunity", key, key, FieldType.Text, false, null, 0, $"field-{tenant.Value}-{key}", Guid.NewGuid()));
        if (deprecate)
            await DeprecateFieldAsync(tenant, created.DefinitionId, created.RowVersion);
        return created.DefinitionId;
    }

    private async Task DeprecateFieldAsync(TenantId tenant, long id, long rowVersion)
    {
        await using var context = fixture.CreateAdminContext();
        await new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(new ManageFieldDefinitionCommand(
            tenant, Administrator, ChangeOperation.Deprecate, id, rowVersion, "crm", "opportunity", "", "", FieldType.Text, false, null, 0, $"deprecate-{tenant.Value}-{id}", Guid.NewGuid()));
    }

    private Task<ManageViewDefinitionResult> ManageViewAsync(
        TenantId tenant, ChangeOperation operation, string key, string name, ViewColumnInput[] columns, long? id = null, long version = 0, string? idempotencyKey = null, IAuthorizer? authorizer = null)
    {
        var context = fixture.CreateAdminContext();
        return new ManageViewDefinitionHandler(context, authorizer ?? StubAuthorizer.AlwaysAllow).HandleAsync(new ManageViewDefinitionCommand(
            tenant, Administrator, operation, id, version, "crm", "opportunity", key, name, columns, 0, idempotencyKey ?? Guid.NewGuid().ToString(), Guid.NewGuid()));
    }

    private static ViewColumnInput Built(string key) => new("builtin", key);
    private static ViewColumnInput Field(string key) => new("field", key);

    private async Task<long[]> EdgesOfAsync(long viewId)
    {
        await using var verify = fixture.CreateAdminContext();
        return (await verify.DependencyEdges.Where(e => e.FromId == viewId).OrderBy(e => e.ToId).ToListAsync()).Select(e => e.ToId).ToArray();
    }

    [Fact]
    public async Task Creating_a_view_publishes_a_change_set_and_computes_one_edge_per_field_column_by_id()
    {
        var tenant = TestTenants.Next();
        var (region, tier) = (await DefineFieldAsync(tenant, "region"), await DefineFieldAsync(tenant, "tier"));

        var created = await ManageViewAsync(tenant, ChangeOperation.Create, "pipeline_review", "Pipeline review", [Built("id"), Field("region"), Field("tier"), Built("status")]);

        Assert.Equal([region, tier], await EdgesOfAsync(created.DefinitionId));
        await using var verify = fixture.CreateAdminContext();
        var view = await verify.ViewDefinitions.AsNoTracking().SingleAsync(v => v.Id == created.DefinitionId);
        Assert.Equal(["builtin:id", "field:region", "field:tier", "builtin:status"], view.Columns.Select(c => $"{c.Kind}:{c.Key}"));
        var set = await verify.ChangeSets.Include(c => c.Items).SingleAsync(c => c.Id == created.ChangeSetId);
        Assert.Equal((ChangeSetStatus.Active, ChangeSetItem.ViewKind), (set.Status, Assert.Single(set.Items).TargetKind));
        Assert.Single(await verify.OutboxMessages.Where(m => m.TenantId == tenant && m.EventType == ChangeSetPublisher.ViewChangedEventType).ToListAsync());
        Assert.Contains(await verify.EvidenceRecords.Where(e => e.TenantId == tenant && e.AggregateType == nameof(CatalogViewDefinition)).ToListAsync(), e => e.Action == "ViewDefinition.Create");
    }

    [Fact]
    public async Task A_column_naming_an_undefined_field_is_refused_and_nothing_is_written()
    {
        var tenant = TestTenants.Next();
        await DefineFieldAsync(tenant, "region");

        await Assert.ThrowsAsync<ViewColumnUnknownException>(() =>
            ManageViewAsync(tenant, ChangeOperation.Create, "broken_view", "Broken", [Built("id"), Field("region"), Field("ghost")]));

        await using var verify = fixture.CreateAdminContext();
        Assert.False(await verify.ViewDefinitions.AnyAsync(v => v.TenantId == tenant));
        Assert.False(await verify.DependencyEdges.AnyAsync(e => e.TenantId == tenant));
        Assert.Equal(1, await verify.ChangeSets.CountAsync(c => c.TenantId == tenant));   // only the field's own set
    }

    [Fact]
    public async Task A_new_or_edited_view_may_not_place_a_deprecated_field()
    {
        var tenant = TestTenants.Next();
        await DefineFieldAsync(tenant, "old_field", deprecate: true);
        await DefineFieldAsync(tenant, "region");
        var view = await ManageViewAsync(tenant, ChangeOperation.Create, "review", "Review", [Field("region")]);

        await Assert.ThrowsAsync<ViewColumnDeprecatedException>(() => ManageViewAsync(tenant, ChangeOperation.Create, "other", "Other", [Field("old_field")]));
        await Assert.ThrowsAsync<ViewColumnDeprecatedException>(() =>
            ManageViewAsync(tenant, ChangeOperation.Update, "", "Review", [Field("region"), Field("old_field")], view.DefinitionId, view.RowVersion));
        Assert.Equal(1, view.RowVersion);
    }

    [Fact]
    public async Task Editing_a_view_recomputes_its_edges_and_a_refused_edit_leaves_them_alone()
    {
        var tenant = TestTenants.Next();
        var (region, tier, owner) = (await DefineFieldAsync(tenant, "region"), await DefineFieldAsync(tenant, "tier"), await DefineFieldAsync(tenant, "owner_team"));
        var created = await ManageViewAsync(tenant, ChangeOperation.Create, "review", "Review", [Field("region"), Field("tier")]);
        Assert.Equal([region, tier], await EdgesOfAsync(created.DefinitionId));

        var updated = await ManageViewAsync(tenant, ChangeOperation.Update, "", "Review", [Field("tier"), Field("owner_team"), Built("id")], created.DefinitionId, created.RowVersion);
        Assert.Equal(2, updated.RowVersion);
        Assert.Equal([tier, owner], await EdgesOfAsync(created.DefinitionId));

        await Assert.ThrowsAsync<ViewColumnUnknownException>(() =>
            ManageViewAsync(tenant, ChangeOperation.Update, "", "Review", [Field("ghost")], created.DefinitionId, updated.RowVersion));
        Assert.Equal([tier, owner], await EdgesOfAsync(created.DefinitionId));
        await Assert.ThrowsAsync<CatalogConcurrencyConflictException>(() =>
            ManageViewAsync(tenant, ChangeOperation.Update, "", "Review", [Built("id")], created.DefinitionId, version: 1));
    }

    [Fact]
    public async Task Deprecating_a_field_a_view_uses_is_allowed_and_the_dependency_is_what_the_reader_reports()
    {
        var tenant = TestTenants.Next();
        var region = await DefineFieldAsync(tenant, "region");
        var tier = await DefineFieldAsync(tenant, "tier");
        var review = await ManageViewAsync(tenant, ChangeOperation.Create, "review", "Review", [Field("region"), Field("tier")]);
        var tierOnly = await ManageViewAsync(tenant, ChangeOperation.Create, "tiers", "Tiers", [Field("tier")]);

        await using var runtime = PostgresFixture.CreateContext(await fixture.RuntimeConnectionStringAsync());
        var reader = new SemanticDefinitionReader(runtime);
        Assert.Equal(["review"], (await reader.ListDependentViewsAsync(tenant, region)).Select(v => v.Key));
        Assert.Equal(["review", "tiers"], (await reader.ListDependentViewsAsync(tenant, tier)).Select(v => v.Key).Order());

        await DeprecateFieldAsync(tenant, region, 1);   // allowed: deprecation is soft
        Assert.Equal([region, tier], await EdgesOfAsync(review.DefinitionId));
        Assert.Equal(["review"], (await reader.ListDependentViewsAsync(tenant, region)).Select(v => v.Key));

        // A deprecated view no longer depends on anything the person needs to be warned about.
        await ManageViewAsync(tenant, ChangeOperation.Deprecate, "", "", [], tierOnly.DefinitionId, tierOnly.RowVersion);
        Assert.Equal(["review"], (await reader.ListDependentViewsAsync(tenant, tier)).Select(v => v.Key));
        await ManageViewAsync(tenant, ChangeOperation.Reactivate, "", "", [], tierOnly.DefinitionId, 2);
        Assert.Equal(["review", "tiers"], (await reader.ListDependentViewsAsync(tenant, tier)).Select(v => v.Key).Order());
    }

    [Fact]
    public async Task The_dependency_reader_is_tenant_scoped_as_the_runtime_role()
    {
        var (tenantA, tenantB) = (TestTenants.Next(), TestTenants.Next());
        var field = await DefineFieldAsync(tenantA, "region");
        await ManageViewAsync(tenantA, ChangeOperation.Create, "review", "Review", [Field("region")]);

        await using var runtime = PostgresFixture.CreateContext(await fixture.RuntimeConnectionStringAsync());
        var reader = new SemanticDefinitionReader(runtime);
        Assert.Single(await reader.ListDependentViewsAsync(tenantA, field));
        Assert.Empty(await reader.ListDependentViewsAsync(tenantB, field));
    }

    [Fact]
    public async Task A_set_can_create_a_field_and_a_view_that_uses_it_in_one_publish()
    {
        var tenant = TestTenants.Next();
        await using var context = fixture.CreateAdminContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenant);
        var publisher = new ChangeSetPublisher(context);
        var set = await publisher.DraftAsync(tenant, Administrator,
        [
            ChangeSetItem.ForField(ChangeOperation.Create, null, new FieldChangeContent("crm", "opportunity", "fresh", "Fresh", FieldType.Text, false, null, 0, 0)),
            ChangeSetItem.ForView(ChangeOperation.Create, null, new ViewChangeContent("crm", "opportunity", "fresh_view", "Fresh view", [new ViewColumn("field", "fresh")], 0, 0))
        ], authorUnderLock: true);
        set.Validate(); set.SubmitForApproval(); set.Approve();

        var outcome = await publisher.PublishAsync(set, Administrator, Guid.NewGuid());
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        Assert.True(outcome.Published);
        Assert.Equal([outcome.Fields.Single().Id], await EdgesOfAsync(outcome.Views.Single().Id));
    }

    [Fact]
    public async Task A_replayed_key_returns_the_stored_response_and_a_reused_key_with_another_body_is_refused()
    {
        var tenant = TestTenants.Next();
        await DefineFieldAsync(tenant, "region");
        var key = $"view-once-{tenant.Value}";

        var first = await ManageViewAsync(tenant, ChangeOperation.Create, "review", "Review", [Field("region")], idempotencyKey: key);
        var replay = await ManageViewAsync(tenant, ChangeOperation.Create, "review", "Review", [Field("region")], idempotencyKey: key);

        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        Assert.Equal((first.DefinitionId, first.ChangeSetId), (replay.DefinitionId, replay.ChangeSetId));
        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() =>
            ManageViewAsync(tenant, ChangeOperation.Create, "review", "A different view", [Field("region")], idempotencyKey: key));
        await using var verify = fixture.CreateAdminContext();
        Assert.Equal(1, await verify.ViewDefinitions.CountAsync(v => v.TenantId == tenant));
    }

    [Fact]
    public async Task A_duplicate_key_and_a_missing_view_map_to_their_exceptions()
    {
        var tenant = TestTenants.Next();
        await ManageViewAsync(tenant, ChangeOperation.Create, "review", "Review", [Built("id")]);

        await Assert.ThrowsAsync<ViewKeyConflictException>(() => ManageViewAsync(tenant, ChangeOperation.Create, "review", "Again", [Built("id")]));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => ManageViewAsync(tenant, ChangeOperation.Update, "", "Nope", [Built("id")], 987_654, 1));
        await Assert.ThrowsAsync<ArgumentException>(() => ManageViewAsync(tenant, ChangeOperation.Update, "", "Nope", [Built("id")], null, 1));
    }

    [Fact]
    public async Task The_fifty_first_active_view_is_refused()
    {
        var tenant = TestTenants.Next();
        await using (var seed = fixture.CreateAdminContext())
        {
            seed.ViewDefinitions.AddRange(Enumerable.Range(0, 50).Select(i => CatalogViewDefinition.Create(tenant, "crm", "opportunity", $"view_{i:00}", $"View {i}", [new ViewColumn("builtin", "id")])));
            await seed.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<ViewLimitExceededException>(() => ManageViewAsync(tenant, ChangeOperation.Create, "one_more", "One more", [Built("id")]));
    }

    [Fact]
    public async Task Views_are_listed_ordered_at_the_read_action_and_managed_at_the_write_action()
    {
        var tenant = TestTenants.Next();
        await ManageViewAsync(tenant, ChangeOperation.Create, "zeta_view", "Zeta", [Built("id")]);
        await ManageViewAsync(tenant, ChangeOperation.Create, "alpha_view", "Alpha", [Built("id"), Built("status")]);

        await using (var context = fixture.CreateAdminContext())
        {
            var listed = await new ListViewDefinitionsHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(new ListViewDefinitionsQuery(tenant, Administrator, "crm", "opportunity", Guid.NewGuid()));
            Assert.Equal(["alpha_view", "zeta_view"], listed.Select(v => v.Key));
            Assert.Equal(["id", "status"], listed[0].Columns.Select(c => c.Key));
            Assert.Equal(("Active", "table"), (listed[0].Status, listed[0].Kind));
        }

        await using (var context = fixture.CreateAdminContext())
            await Assert.ThrowsAsync<CatalogAuthorizationDeniedException>(() =>
                new ListViewDefinitionsHandler(context, StubAuthorizer.AlwaysDeny).HandleAsync(new ListViewDefinitionsQuery(tenant, Administrator, "crm", "opportunity", Guid.NewGuid())));
        await Assert.ThrowsAsync<CatalogAuthorizationDeniedException>(() =>
            ManageViewAsync(tenant, ChangeOperation.Create, "denied", "Denied", [Built("id")], authorizer: StubAuthorizer.AlwaysDeny));

        var recorder = new RecordingAuthorizer();
        await ManageViewAsync(tenant, ChangeOperation.Create, "recorded", "Recorded", [Built("id")], authorizer: recorder);
        await using (var context = fixture.CreateAdminContext())
            await new ListViewDefinitionsHandler(context, recorder).HandleAsync(new ListViewDefinitionsQuery(tenant, Administrator, "crm", "opportunity", Guid.NewGuid()));
        Assert.Equal(["crm.settings.update", "crm.settings.read"], recorder.Requests.Select(r => r.Action.Value));
        Assert.All(recorder.Requests, r => Assert.Equal("CrmSettings", r.Resource.ResourceType));
    }

    [Fact]
    public async Task The_runtime_role_sees_only_its_own_tenants_views_and_edges()
    {
        var (tenantA, tenantB) = (TestTenants.Next(), TestTenants.Next());
        foreach (var tenant in new[] { tenantA, tenantB })
        {
            await DefineFieldAsync(tenant, "region");
            await ManageViewAsync(tenant, ChangeOperation.Create, "review", "Review", [Field("region")]);
        }

        await using var runtime = PostgresFixture.CreateContext(await fixture.RuntimeConnectionStringAsync());
        Assert.Empty(await runtime.ViewDefinitions.AsNoTracking().ToListAsync());
        Assert.Empty(await runtime.DependencyEdges.AsNoTracking().ToListAsync());

        await using var transaction = await runtime.Database.BeginTransactionAsync();
        await runtime.SetTenantContextAsync(tenantB);
        Assert.Equal(tenantB, Assert.Single(await runtime.ViewDefinitions.AsNoTracking().ToListAsync()).TenantId);
        Assert.Equal(tenantB, Assert.Single(await runtime.DependencyEdges.AsNoTracking().ToListAsync()).TenantId);
    }

    [Theory]
    [InlineData("UPDATE semantic.view_definitions SET kind = 'board'", "ck_view_definitions_kind")]
    [InlineData("UPDATE semantic.view_definitions SET columns = '[]'::jsonb", "ck_view_definitions_columns")]
    [InlineData("UPDATE semantic.view_definitions SET columns = '{}'::jsonb", "ck_view_definitions_columns")]
    [InlineData("UPDATE semantic.view_definitions SET status = 'Gone'", "ck_view_definitions_status")]
    [InlineData("UPDATE semantic.view_definitions SET key = 'Bad Key'", "ck_view_definitions_key")]
    [InlineData("UPDATE semantic.dependency_edges SET to_kind = 'view'", "ck_dependency_edges_to_kind")]
    public async Task The_database_enforces_the_view_and_edge_shape(string sql, string constraint)
    {
        var tenant = TestTenants.Next();
        await DefineFieldAsync(tenant, "region");
        await ManageViewAsync(tenant, ChangeOperation.Create, "review", "Review", [Field("region")]);

        await using var connection = new NpgsqlConnection(fixture.AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"{sql} WHERE tenant_id = {tenant.Value}", connection);
        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(constraint, exception.ConstraintName);
    }

    [Fact]
    public async Task An_edge_cannot_point_at_a_field_that_does_not_exist_and_dies_with_its_view()
    {
        var tenant = TestTenants.Next();
        var field = await DefineFieldAsync(tenant, "region");
        var view = await ManageViewAsync(tenant, ChangeOperation.Create, "review", "Review", [Field("region")]);

        await using var connection = new NpgsqlConnection(fixture.AdminConnectionString);
        await connection.OpenAsync();
        await using (var dangling = new NpgsqlCommand(
            $"INSERT INTO semantic.dependency_edges (tenant_id, from_kind, from_id, to_kind, to_id) VALUES ({tenant.Value}, 'view', {view.DefinitionId}, 'field', 999999999)", connection))
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, (await Assert.ThrowsAsync<PostgresException>(() => dangling.ExecuteNonQueryAsync())).SqlState);
        await using (var removeField = new NpgsqlCommand($"DELETE FROM semantic.field_definitions WHERE id = {field}", connection))
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, (await Assert.ThrowsAsync<PostgresException>(() => removeField.ExecuteNonQueryAsync())).SqlState);
        await using (var removeView = new NpgsqlCommand($"DELETE FROM semantic.view_definitions WHERE id = {view.DefinitionId}", connection))
            await removeView.ExecuteNonQueryAsync();
        Assert.Empty(await EdgesOfAsync(view.DefinitionId));
    }
}
