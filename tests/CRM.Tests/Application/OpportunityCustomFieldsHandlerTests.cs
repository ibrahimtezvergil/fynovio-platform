using System.Text.Json;
using Contracts;
using CRM.Application;
using CRM.Customization;
using CRM.Domain;
using CRM.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Application;

[Collection(nameof(PostgresCollection))]
public sealed class OpportunityCustomFieldsHandlerTests
{
    private readonly PostgresFixture _fixture;
    private readonly StubDefinitionReader _definitions = new();

    public OpportunityCustomFieldsHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public async Task Create_stores_validated_custom_fields_in_canonical_form()
    {
        var tenant = TestData.NextTenant();
        SeedFields(tenant);
        var partyRef = new PartyRef(tenant, 1);
        var command = new CreateOpportunityCommand(tenant, partyRef, TestData.Seller, "TRY", 1000m, "create-cf", Guid.NewGuid())
        {
            CustomFields = Json("""{"budget_code": "B-7", "priority": "high"}""")
        };

        await using var context = _fixture.CreateAdminContext();
        var result = await new CreateOpportunityHandler(context, StubAuthorizer.AlwaysAllow, StubPartyIdentityResolver.Identity, _definitions).HandleAsync(command);

        var stored = await context.Opportunities.AsNoTracking().SingleAsync(o => o.Id == result.OpportunityId);
        var values = CustomFieldValues.Parse(stored.CustomFields);
        Assert.Equal("B-7", values["budget_code"].GetString());
        Assert.Equal("high", values["priority"].GetString());
    }

    [Fact]
    public async Task Create_rejects_a_missing_required_field_and_an_unknown_key()
    {
        var tenant = TestData.NextTenant();
        SeedFields(tenant);
        var command = new CreateOpportunityCommand(tenant, new PartyRef(tenant, 1), TestData.Seller, "TRY", 1000m, "create-invalid", Guid.NewGuid())
        {
            CustomFields = Json("""{"nope": 1}""")
        };

        await using var context = _fixture.CreateAdminContext();
        var exception = await Assert.ThrowsAsync<CustomFieldValidationException>(() =>
            new CreateOpportunityHandler(context, StubAuthorizer.AlwaysAllow, StubPartyIdentityResolver.Identity, _definitions).HandleAsync(command));

        Assert.Contains(exception.Errors, e => e.Field == "budget_code" && e.Code == "required");
        Assert.Contains(exception.Errors, e => e.Field == "nope" && e.Code == "unknown_field");
        Assert.False(await context.Opportunities.AnyAsync(o => o.TenantId == tenant));
    }

    [Fact]
    public async Task Create_with_the_same_key_but_different_custom_fields_is_a_key_reuse()
    {
        var tenant = TestData.NextTenant();
        SeedFields(tenant);
        var command = new CreateOpportunityCommand(tenant, new PartyRef(tenant, 1), TestData.Seller, "TRY", 1000m, "create-reuse", Guid.NewGuid())
        {
            CustomFields = Json("""{"budget_code": "A"}""")
        };

        await using (var first = _fixture.CreateAdminContext())
            await new CreateOpportunityHandler(first, StubAuthorizer.AlwaysAllow, StubPartyIdentityResolver.Identity, _definitions).HandleAsync(command);

        await using var second = _fixture.CreateAdminContext();
        var replay = await new CreateOpportunityHandler(second, StubAuthorizer.AlwaysAllow, StubPartyIdentityResolver.Identity, _definitions).HandleAsync(command);
        Assert.True(replay.Replayed);

        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() =>
            new CreateOpportunityHandler(second, StubAuthorizer.AlwaysAllow, StubPartyIdentityResolver.Identity, _definitions)
                .HandleAsync(command with { CustomFields = Json("""{"budget_code": "B"}""") }));
    }

    [Fact]
    public async Task Update_replaces_values_bumps_the_version_and_emits_changed_keys_only()
    {
        var tenant = TestData.NextTenant();
        SeedFields(tenant);
        var opportunity = await SeedOpportunityAsync(tenant, """{"budget_code": "A", "priority": "low"}""");

        var command = new UpdateOpportunityCustomFieldsCommand(tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion,
            Json("""{"budget_code": "A", "priority": "high"}"""), "update-1", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        var result = await new UpdateOpportunityCustomFieldsHandler(context, StubAuthorizer.AlwaysAllow, _definitions).HandleAsync(command);

        Assert.False(result.Replayed);
        var reloaded = await context.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunity.Id);
        Assert.Equal(opportunity.RowVersion + 1, reloaded.RowVersion);
        Assert.Equal("high", CustomFieldValues.Parse(reloaded.CustomFields)["priority"].GetString());

        var message = Assert.Single(await context.OutboxMessages.AsNoTracking()
            .Where(m => m.TenantId == tenant && m.AggregateId == opportunity.Id).ToListAsync());
        Assert.Equal("enterprise.crmsales.opportunity.custom_fields_changed.v1", message.EventType);
        using var payload = JsonDocument.Parse(message.Payload);
        Assert.Equal(["priority"], payload.RootElement.GetProperty("ChangedKeys").EnumerateArray().Select(k => k.GetString()!).ToArray());
        Assert.DoesNotContain("high", message.Payload);
        Assert.Empty(await context.EvidenceRecords.AsNoTracking().Where(e => e.TenantId == tenant && e.AggregateId == opportunity.Id).ToListAsync());
    }

    [Fact]
    public async Task Update_that_changes_nothing_keeps_the_version_and_emits_nothing()
    {
        var tenant = TestData.NextTenant();
        SeedFields(tenant);
        var opportunity = await SeedOpportunityAsync(tenant, """{"budget_code": "A", "tags": ["x", "y"]}""");

        var command = new UpdateOpportunityCustomFieldsCommand(tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion,
            Json("""{"tags":["x","y"],"budget_code":"A"}"""), "update-noop", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        await new UpdateOpportunityCustomFieldsHandler(context, StubAuthorizer.AlwaysAllow, _definitions).HandleAsync(command);

        var reloaded = await context.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunity.Id);
        Assert.Equal(opportunity.RowVersion, reloaded.RowVersion);
        Assert.Empty(await context.OutboxMessages.AsNoTracking().Where(m => m.TenantId == tenant && m.AggregateId == opportunity.Id).ToListAsync());
    }

    [Fact]
    public async Task Update_replays_and_rejects_a_reused_key_with_a_different_body()
    {
        var tenant = TestData.NextTenant();
        SeedFields(tenant);
        var opportunity = await SeedOpportunityAsync(tenant, """{"budget_code": "A"}""");
        var command = new UpdateOpportunityCustomFieldsCommand(tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion,
            Json("""{"budget_code": "B"}"""), "update-replay", Guid.NewGuid());

        await using (var first = _fixture.CreateAdminContext())
            await new UpdateOpportunityCustomFieldsHandler(first, StubAuthorizer.AlwaysAllow, _definitions).HandleAsync(command);

        await using var second = _fixture.CreateAdminContext();
        var replay = await new UpdateOpportunityCustomFieldsHandler(second, StubAuthorizer.AlwaysAllow, _definitions).HandleAsync(command);
        Assert.True(replay.Replayed);
        Assert.Single(await second.OutboxMessages.AsNoTracking().Where(m => m.TenantId == tenant && m.AggregateId == opportunity.Id).ToListAsync());

        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() =>
            new UpdateOpportunityCustomFieldsHandler(second, StubAuthorizer.AlwaysAllow, _definitions)
                .HandleAsync(command with { CustomFields = Json("""{"budget_code": "C"}""") }));
    }

    [Fact]
    public async Task Update_rejects_a_deprecated_key_but_carries_its_stored_value_forward()
    {
        var tenant = TestData.NextTenant();
        var (_, priority) = SeedFields(tenant);
        var opportunity = await SeedOpportunityAsync(tenant, """{"budget_code": "A", "priority": "low"}""");
        _definitions.Change(priority, d => d with { Status = FieldStatus.Deprecated });

        await using var context = _fixture.CreateAdminContext();
        var handler = new UpdateOpportunityCustomFieldsHandler(context, StubAuthorizer.AlwaysAllow, _definitions);
        var rejected = await Assert.ThrowsAsync<CustomFieldValidationException>(() => handler.HandleAsync(
            new UpdateOpportunityCustomFieldsCommand(tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion,
                Json("""{"budget_code": "A", "priority": "high"}"""), "update-deprecated", Guid.NewGuid())));
        Assert.Contains(rejected.Errors, e => e.Field == "priority" && e.Code == "field_deprecated");

        await handler.HandleAsync(new UpdateOpportunityCustomFieldsCommand(tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion,
            Json("""{"budget_code": "B"}"""), "update-without-deprecated", Guid.NewGuid()));

        var reloaded = await context.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunity.Id);
        var values = CustomFieldValues.Parse(reloaded.CustomFields);
        Assert.Equal("B", values["budget_code"].GetString());
        Assert.Equal("low", values["priority"].GetString());
    }

    [Fact]
    public async Task Update_is_refused_on_an_archived_opportunity()
    {
        var tenant = TestData.NextTenant();
        SeedFields(tenant);
        var opportunity = await SeedOpportunityAsync(tenant, """{"budget_code": "A"}""", archive: true);

        await using var context = _fixture.CreateAdminContext();
        var handler = new UpdateOpportunityCustomFieldsHandler(context, StubAuthorizer.AlwaysAllow, _definitions);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(new UpdateOpportunityCustomFieldsCommand(
            tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion, Json("""{"budget_code": "B"}"""), "update-archived", Guid.NewGuid())));
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(new UpdateOpportunityCustomFieldsCommand(
            tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion, Json("""{"budget_code": "A"}"""), "update-archived-noop", Guid.NewGuid())));
    }

    [Fact]
    public async Task Update_is_denied_without_the_action_and_conflicts_on_a_stale_version()
    {
        var tenant = TestData.NextTenant();
        SeedFields(tenant);
        var opportunity = await SeedOpportunityAsync(tenant, """{"budget_code": "A"}""");

        await using var context = _fixture.CreateAdminContext();
        var denied = await Assert.ThrowsAsync<OpportunityAuthorizationDeniedException>(() =>
            new UpdateOpportunityCustomFieldsHandler(context, StubAuthorizer.AlwaysDeny, _definitions).HandleAsync(new UpdateOpportunityCustomFieldsCommand(
                tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion, Json("""{"budget_code": "B"}"""), "update-denied", Guid.NewGuid())));
        Assert.Equal(CrmActionKeys.OpportunityUpdateCustomFields, denied.ActionKey);

        await Assert.ThrowsAsync<OpportunityConcurrencyConflictException>(() =>
            new UpdateOpportunityCustomFieldsHandler(context, StubAuthorizer.AlwaysAllow, _definitions).HandleAsync(new UpdateOpportunityCustomFieldsCommand(
                tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion + 5, Json("""{"budget_code": "B"}"""), "update-stale", Guid.NewGuid())));
    }

    [Fact]
    public async Task Get_exposes_the_stored_custom_fields()
    {
        var tenant = TestData.NextTenant();
        SeedFields(tenant);
        var opportunity = await SeedOpportunityAsync(tenant, """{"budget_code": "A"}""");

        await using var context = _fixture.CreateAdminContext();
        var dto = await new GetOpportunityHandler(context, StubAuthorizer.AlwaysAllow)
            .HandleAsync(new GetOpportunityQuery(tenant, opportunity.Id, TestData.Seller, Guid.NewGuid()));

        Assert.NotNull(dto);
        Assert.Equal("A", dto.CustomFields!.Value.GetProperty("budget_code").GetString());
    }

    [Fact]
    public async Task List_filters_by_select_multi_select_and_boolean_values_within_the_callers_scope()
    {
        var tenant = TestData.NextTenant();
        SeedFields(tenant);
        _definitions.Add(TestFields.Create(tenant, "vip", "VIP", FieldType.Boolean));

        var highTagged = await SeedOpportunityAsync(tenant, """{"budget_code": "A", "priority": "high", "tags": ["x", "y"], "vip": true}""");
        var high = await SeedOpportunityAsync(tenant, """{"budget_code": "B", "priority": "high", "vip": false}""");
        await SeedOpportunityAsync(tenant, """{"budget_code": "C", "priority": "low", "tags": ["x"]}""");
        await SeedOpportunityAsync(tenant, """{"budget_code": "D"}""");
        var otherOwner = Opportunity.Create(tenant, new PartyRef(tenant, 1), new PrincipalRef("https://idp.local", "seller-2"), "TRY", 1m,
            customFields: """{"budget_code": "E", "priority": "high"}""");
        await using (var admin = _fixture.CreateAdminContext())
        {
            admin.Opportunities.Add(otherOwner);
            await admin.SaveChangesAsync();
        }

        async Task<long[]> ListAsync(AccessScope scope, Dictionary<string, string> filters)
        {
            await using var context = _fixture.CreateAdminContext();
            var rows = await new ListOpportunitiesHandler(context, new StubScopeResolver(scope), _definitions)
                .HandleAsync(new ListOpportunitiesQuery(tenant, TestData.Seller, Guid.NewGuid(), null, 0, 50, CustomFieldFilters: filters));
            return rows.Select(r => r.Id).Order().ToArray();
        }

        var all = new AccessScope.All();
        Assert.Equal(new[] { highTagged.Id, high.Id, otherOwner.Id }.Order(), await ListAsync(all, new() { ["priority"] = "high" }));
        Assert.Equal([highTagged.Id], await ListAsync(all, new() { ["priority"] = "high", ["tags"] = "y" }));
        Assert.Equal([high.Id], await ListAsync(all, new() { ["vip"] = "false" }));
        Assert.Equal(new[] { highTagged.Id, high.Id }.Order(),
            await ListAsync(new AccessScope.AnyOf([new ScopeTerm.OwnedBy(TestData.Seller)]), new() { ["priority"] = "high" }));
    }

    [Fact]
    public async Task List_rejects_unknown_non_filterable_and_out_of_range_filters()
    {
        var tenant = TestData.NextTenant();
        SeedFields(tenant);

        await using var context = _fixture.CreateAdminContext();
        var exception = await Assert.ThrowsAsync<CustomFieldValidationException>(() =>
            new ListOpportunitiesHandler(context, new StubScopeResolver(new AccessScope.All()), _definitions).HandleAsync(new ListOpportunitiesQuery(
                tenant, TestData.Seller, Guid.NewGuid(), null, 0, 50,
                CustomFieldFilters: new Dictionary<string, string> { ["nope"] = "1", ["budget_code"] = "A", ["priority"] = "urgent" })));

        Assert.Contains(exception.Errors, e => e.Field == "nope" && e.Code == "unknown_field");
        Assert.Contains(exception.Errors, e => e.Field == "budget_code" && e.Code == "not_filterable");
        Assert.Contains(exception.Errors, e => e.Field == "priority" && e.Code == "invalid_option");
    }

    /// <summary>budget_code (text, required), priority (select), tags (multi_select).</summary>
    private (long BudgetCode, long Priority) SeedFields(TenantId tenant)
    {
        var budgetCode = _definitions.Add(TestFields.Create(tenant, "budget_code", "Budget code", FieldType.Text, isRequired: true));
        var priority = _definitions.Add(TestFields.Create(tenant, "priority", "Priority", FieldType.Select,
            config: new FieldConfig(Options: [new FieldOption("low", "Low"), new FieldOption("high", "High")])));
        _definitions.Add(TestFields.Create(tenant, "tags", "Tags", FieldType.MultiSelect,
            config: new FieldConfig(Options: [new FieldOption("x", "X"), new FieldOption("y", "Y")])));
        return (budgetCode.Id, priority.Id);
    }

    private async Task<Opportunity> SeedOpportunityAsync(TenantId tenant, string customFields, bool archive = false)
    {
        await using var admin = _fixture.CreateAdminContext();
        var opportunity = Opportunity.Create(tenant, new PartyRef(tenant, 1), TestData.Seller, "TRY", 1000m, customFields: customFields);
        if (archive)
            opportunity.Archive(confirmOpenOpportunity: false);
        admin.Opportunities.Add(opportunity);
        await admin.SaveChangesAsync();
        return opportunity;
    }
}
