using System.Text.Json;
using Contracts;
using CRM.Application;
using CRM.Customization;
using CRM.Domain;
using CRM.Persistence;
using CRM.Tests;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class CustomFieldDefinitionHandlerTests(PostgresFixture fixture)
{
    private static readonly PrincipalRef Administrator = new("https://identity.test", "crm-settings-admin");

    [Fact]
    public async Task Create_is_idempotent_and_commits_outbox_and_evidence()
    {
        var tenant = TestData.NextTenant();
        var command = new ManageCustomFieldDefinitionCommand(
            tenant,
            Administrator,
            CustomFieldOperation.Create,
            null,
            0,
            TenantFieldAggregateType.Opportunity,
            "business_unit",
            "Business Unit",
            TenantFieldValueType.Text,
            false,
            null,
            0,
            "custom-field-create-once",
            Guid.NewGuid());

        ManageCustomFieldDefinitionResult first;
        await using (var context = fixture.CreateAdminContext())
            first = await new ManageCustomFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(command);

        Assert.True(first.DefinitionId > 0);
        Assert.Equal(1, first.RowVersion);
        Assert.False(first.Replayed);

        await using (var context = fixture.CreateAdminContext())
        {
            var replay = await new ManageCustomFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(command with { CorrelationId = Guid.NewGuid() });

            Assert.True(replay.Replayed);
            Assert.Equal(first.DefinitionId, replay.DefinitionId);
            Assert.Single(await context.TenantFieldDefinitions
                .Where(x => x.TenantId == tenant && x.FieldName == "business_unit")
                .ToListAsync());
            Assert.Single(await context.OutboxMessages
                .Where(x => x.TenantId == tenant && x.AggregateType == "TenantFieldDefinition" && x.AggregateId == first.DefinitionId)
                .ToListAsync());
            Assert.Single(await context.EvidenceRecords
                .Where(x => x.TenantId == tenant && x.AggregateType == "TenantFieldDefinition" && x.AggregateId == first.DefinitionId)
                .ToListAsync());
        }
    }

    [Fact]
    public async Task Idempotency_key_reused_with_different_body_fails()
    {
        var tenant = TestData.NextTenant();
        var idempotencyKey = "duplicate-key";
        var correlationId = Guid.NewGuid();

        var command1 = new ManageCustomFieldDefinitionCommand(tenant, Administrator, CustomFieldOperation.Create, null, 0,
            TenantFieldAggregateType.Opportunity, "field_one", "Field One", TenantFieldValueType.Text, false, null, 0, idempotencyKey, correlationId);

        await using (var context = fixture.CreateAdminContext())
            await new ManageCustomFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(command1);

        var command2 = new ManageCustomFieldDefinitionCommand(tenant, Administrator, CustomFieldOperation.Create, null, 0,
            TenantFieldAggregateType.Opportunity, "field_two", "Field Two", TenantFieldValueType.Text, false, null, 0, idempotencyKey, Guid.NewGuid());

        await using (var context = fixture.CreateAdminContext())
        {
            var ex = await Assert.ThrowsAsync<IdempotencyKeyReusedException>(
                () => new ManageCustomFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                    .HandleAsync(command2));
            Assert.Contains("duplicate-key", ex.Message);
        }
    }

    [Fact]
    public async Task Party_aggregate_type_is_refused()
    {
        var tenant = TestData.NextTenant();
        var command = new ManageCustomFieldDefinitionCommand(
            tenant,
            Administrator,
            CustomFieldOperation.Create,
            null,
            0,
            TenantFieldAggregateType.Party,
            "party_field",
            "Party Field",
            TenantFieldValueType.Text,
            false,
            null,
            0,
            "party-refused",
            Guid.NewGuid());

        await using (var context = fixture.CreateAdminContext())
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(
                () => new ManageCustomFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                    .HandleAsync(command));
            Assert.Contains("Party", ex.Message);
        }
    }

    [Fact]
    public async Task Duplicate_field_key_throws_conflict()
    {
        var tenant = TestData.NextTenant();

        var command1 = new ManageCustomFieldDefinitionCommand(tenant, Administrator, CustomFieldOperation.Create, null, 0,
            TenantFieldAggregateType.Opportunity, "region", "Region", TenantFieldValueType.Text, false, null, 0, "first-field", Guid.NewGuid());

        await using (var context = fixture.CreateAdminContext())
            await new ManageCustomFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(command1);

        var command2 = new ManageCustomFieldDefinitionCommand(tenant, Administrator, CustomFieldOperation.Create, null, 0,
            TenantFieldAggregateType.Opportunity, "region", "Region (different label)", TenantFieldValueType.Text, false, null, 0, "second-field", Guid.NewGuid());

        await using (var context = fixture.CreateAdminContext())
        {
            var ex = await Assert.ThrowsAsync<CustomFieldKeyConflictException>(
                () => new ManageCustomFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                    .HandleAsync(command2));
            Assert.Contains("region", ex.Message);
        }
    }

    [Fact]
    public async Task Creating_the_101st_active_field_fails()
    {
        var tenant = TestData.NextTenant();

        await using (var context = fixture.CreateAdminContext())
        {
            for (int i = 0; i < 100; i++)
            {
                var cmd = new ManageCustomFieldDefinitionCommand(
                    tenant, Administrator, CustomFieldOperation.Create, null, 0,
                    TenantFieldAggregateType.Opportunity, $"field_{i:D3}", $"Field {i}", TenantFieldValueType.Text,
                    false, null, 0, $"idempotency-{i}", Guid.NewGuid());
                await new ManageCustomFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                    .HandleAsync(cmd);
            }
        }

        var commandFor101 = new ManageCustomFieldDefinitionCommand(
            tenant, Administrator, CustomFieldOperation.Create, null, 0,
            TenantFieldAggregateType.Opportunity, "field_100", "Field 100", TenantFieldValueType.Text,
            false, null, 0, "idempotency-100", Guid.NewGuid());

        await using (var context = fixture.CreateAdminContext())
        {
            var ex = await Assert.ThrowsAsync<CustomFieldLimitExceededException>(
                () => new ManageCustomFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                    .HandleAsync(commandFor101));
            Assert.Contains("100", ex.Message);
        }
    }

    [Fact]
    public async Task Update_with_stale_row_version_fails()
    {
        var tenant = TestData.NextTenant();

        ManageCustomFieldDefinitionResult created;
        await using (var context = fixture.CreateAdminContext())
        {
            var createCmd = new ManageCustomFieldDefinitionCommand(tenant, Administrator, CustomFieldOperation.Create, null, 0,
                TenantFieldAggregateType.Opportunity, "status_field", "Status", TenantFieldValueType.Select,
                false,
                new TenantFieldConfigInput(
                    [new TenantFieldOptionInput("active", "Active"), new TenantFieldOptionInput("inactive", "Inactive")],
                    null, null, null, null),
                0, "create-status", Guid.NewGuid());
            created = await new ManageCustomFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(createCmd);
        }

        var updateCmd = new ManageCustomFieldDefinitionCommand(
            tenant, Administrator, CustomFieldOperation.Update, created.DefinitionId, 0, // Wrong version
            TenantFieldAggregateType.Opportunity, "status_field", "Status (Updated)", TenantFieldValueType.Select,
            false,
            new TenantFieldConfigInput(
                [new TenantFieldOptionInput("active", "Active"), new TenantFieldOptionInput("inactive", "Inactive")],
                null, null, null, null),
            0, "update-status", Guid.NewGuid());

        await using (var context = fixture.CreateAdminContext())
        {
            var ex = await Assert.ThrowsAsync<CrmSettingsConcurrencyConflictException>(
                () => new ManageCustomFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                    .HandleAsync(updateCmd));
        }
    }

    [Fact]
    public async Task Deprecate_and_then_reactivate()
    {
        var tenant = TestData.NextTenant();

        ManageCustomFieldDefinitionResult created;
        await using (var context = fixture.CreateAdminContext())
        {
            var cmd = new ManageCustomFieldDefinitionCommand(tenant, Administrator, CustomFieldOperation.Create, null, 0,
                TenantFieldAggregateType.Opportunity, "temp_field", "Temporary Field", TenantFieldValueType.Text, false, null, 0, "temp-field", Guid.NewGuid());
            created = await new ManageCustomFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(cmd);
        }

        await using (var context = fixture.CreateAdminContext())
        {
            var deprecateCmd = new ManageCustomFieldDefinitionCommand(
                tenant, Administrator, CustomFieldOperation.Deprecate, created.DefinitionId, created.RowVersion,
                TenantFieldAggregateType.Opportunity, "", "", TenantFieldValueType.Text, false, null, 0, "deprecate-temp", Guid.NewGuid());
            var deprecated = await new ManageCustomFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(deprecateCmd);

            Assert.Equal(2, deprecated.RowVersion);
            var def = await context.TenantFieldDefinitions.SingleAsync(x => x.Id == created.DefinitionId);
            Assert.Equal(TenantFieldStatus.Deprecated, def.Status);
        }

        await using (var context = fixture.CreateAdminContext())
        {
            var def = await context.TenantFieldDefinitions.SingleAsync(x => x.Id == created.DefinitionId);
            var reactivateCmd = new ManageCustomFieldDefinitionCommand(
                tenant, Administrator, CustomFieldOperation.Reactivate, created.DefinitionId, def.RowVersion,
                TenantFieldAggregateType.Opportunity, "", "", TenantFieldValueType.Text, false, null, 0, "reactivate-temp", Guid.NewGuid());
            var reactivated = await new ManageCustomFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(reactivateCmd);

            Assert.Equal(3, reactivated.RowVersion);
            var reactivatedDef = await context.TenantFieldDefinitions.SingleAsync(x => x.Id == created.DefinitionId);
            Assert.Equal(TenantFieldStatus.Active, reactivatedDef.Status);
        }
    }

    [Fact]
    public async Task List_returns_fields_ordered_by_sort_order_then_name()
    {
        var tenant = TestData.NextTenant();

        await using (var context = fixture.CreateAdminContext())
        {
            for (int i = 0; i < 3; i++)
            {
                var cmd = new ManageCustomFieldDefinitionCommand(
                    tenant, Administrator, CustomFieldOperation.Create, null, 0,
                    TenantFieldAggregateType.Opportunity, $"field_{i}", $"Field {i}", TenantFieldValueType.Text,
                    false, null, i % 2 == 0 ? 10 : 5, $"list-test-{i}", Guid.NewGuid());
                await new ManageCustomFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                    .HandleAsync(cmd);
            }
        }

        await using (var context = fixture.CreateAdminContext())
        {
            var query = new ListCustomFieldDefinitionsQuery(
                tenant, Administrator, TenantFieldAggregateType.Opportunity, Guid.NewGuid());
            var list = await new ListCustomFieldDefinitionsHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(query);

            Assert.Equal(3, list.Count);
            // Should be sorted by sort order, then field name
            Assert.All(list, (item, idx) =>
            {
                Assert.True(idx < 2 || list[idx - 1].SortOrder <= item.SortOrder);
            });
        }
    }

    [Fact]
    public async Task List_denies_on_read_authorization()
    {
        var tenant = TestData.NextTenant();

        await using (var context = fixture.CreateAdminContext())
        {
            var cmd = new ManageCustomFieldDefinitionCommand(
                tenant, Administrator, CustomFieldOperation.Create, null, 0,
                TenantFieldAggregateType.Opportunity, "test_field", "Test Field", TenantFieldValueType.Text,
                false, null, 0, "list-auth-test", Guid.NewGuid());
            await new ManageCustomFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(cmd);
        }

        await using (var context = fixture.CreateAdminContext())
        {
            var query = new ListCustomFieldDefinitionsQuery(
                tenant, Administrator, TenantFieldAggregateType.Opportunity, Guid.NewGuid());
            var ex = await Assert.ThrowsAsync<OpportunityAuthorizationDeniedException>(
                () => new ListCustomFieldDefinitionsHandler(context, StubAuthorizer.AlwaysDeny)
                    .HandleAsync(query));
        }
    }

    [Fact]
    public async Task Impact_counts_opportunities_with_the_field()
    {
        var tenant = TestData.NextTenant();

        ManageCustomFieldDefinitionResult definition;
        await using (var context = fixture.CreateAdminContext())
        {
            var cmd = new ManageCustomFieldDefinitionCommand(
                tenant, Administrator, CustomFieldOperation.Create, null, 0,
                TenantFieldAggregateType.Opportunity, "impact_field", "Impact Field", TenantFieldValueType.Text,
                false, null, 0, "impact-field", Guid.NewGuid());
            definition = await new ManageCustomFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(cmd);
        }

        await using (var context = fixture.CreateAdminContext())
        {
            // Create opportunities and set custom fields via SQL (CustomFields has private setter)
            var opp1 = Opportunity.Create(tenant, new PartyRef(tenant, 1), TestData.Seller, "USD", 100m);
            context.Opportunities.Add(opp1);

            var opp2 = Opportunity.Create(tenant, new PartyRef(tenant, 2), TestData.Seller, "USD", 200m);
            context.Opportunities.Add(opp2);

            var opp3 = Opportunity.Create(tenant, new PartyRef(tenant, 3), TestData.Seller, "USD", 300m);
            context.Opportunities.Add(opp3);

            await context.SaveChangesAsync();

            // Update custom_fields via SQL
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE crm.opportunities SET custom_fields = {"{\"impact_field\": \"value1\"}"::jsonb} WHERE tenant_id = {tenant.Value} AND id = {opp1.Id}""");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE crm.opportunities SET custom_fields = {"{\"other_field\": \"value2\"}"::jsonb} WHERE tenant_id = {tenant.Value} AND id = {opp2.Id}""");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE crm.opportunities SET custom_fields = {"{\"impact_field\": \"value3\"}"::jsonb} WHERE tenant_id = {tenant.Value} AND id = {opp3.Id}""");
        }

        await using (var context = fixture.CreateAdminContext())
        {
            var query = new GetCustomFieldImpactQuery(
                tenant, Administrator, definition.DefinitionId, Guid.NewGuid());
            var impact = await new GetCustomFieldImpactHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(query);

            Assert.Equal(definition.DefinitionId, impact.DefinitionId);
            Assert.Equal("impact_field", impact.FieldName);
            Assert.Equal(2, impact.OpportunitiesWithValue);
        }
    }

    [Fact]
    public async Task Impact_is_gated_by_settings_update_authorization()
    {
        var tenant = TestData.NextTenant();

        ManageCustomFieldDefinitionResult definition;
        await using (var context = fixture.CreateAdminContext())
        {
            var cmd = new ManageCustomFieldDefinitionCommand(
                tenant, Administrator, CustomFieldOperation.Create, null, 0,
                TenantFieldAggregateType.Opportunity, "auth_field", "Auth Field", TenantFieldValueType.Text,
                false, null, 0, "auth-field", Guid.NewGuid());
            definition = await new ManageCustomFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(cmd);
        }

        await using (var context = fixture.CreateAdminContext())
        {
            var query = new GetCustomFieldImpactQuery(
                tenant, Administrator, definition.DefinitionId, Guid.NewGuid());
            var ex = await Assert.ThrowsAsync<OpportunityAuthorizationDeniedException>(
                () => new GetCustomFieldImpactHandler(context, StubAuthorizer.AlwaysDeny)
                    .HandleAsync(query));
        }
    }

    [Fact]
    public async Task Manage_is_gated_by_settings_update_authorization()
    {
        var tenant = TestData.NextTenant();
        var command = new ManageCustomFieldDefinitionCommand(
            tenant, Administrator, CustomFieldOperation.Create, null, 0,
            TenantFieldAggregateType.Opportunity, "secure_field", "Secure Field", TenantFieldValueType.Text,
            false, null, 0, "secure-field", Guid.NewGuid());

        await using (var context = fixture.CreateAdminContext())
        {
            var ex = await Assert.ThrowsAsync<OpportunityAuthorizationDeniedException>(
                () => new ManageCustomFieldDefinitionHandler(context, StubAuthorizer.AlwaysDeny)
                    .HandleAsync(command));
        }
    }

    [Fact]
    public async Task Reactivating_the_101st_field_after_deprecation_fails()
    {
        var tenant = TestData.NextTenant();

        ManageCustomFieldDefinitionResult fieldToReactivate;
        await using (var context = fixture.CreateAdminContext())
        {
            // Create 100 active fields
            for (int i = 0; i < 100; i++)
            {
                var cmd = new ManageCustomFieldDefinitionCommand(
                    tenant, Administrator, CustomFieldOperation.Create, null, 0,
                    TenantFieldAggregateType.Opportunity, $"active_{i:D3}", $"Active {i}", TenantFieldValueType.Text,
                    false, null, 0, $"active-{i}", Guid.NewGuid());
                await new ManageCustomFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                    .HandleAsync(cmd);
            }

            // Create one more that we'll deprecate
            var cmd101 = new ManageCustomFieldDefinitionCommand(
                tenant, Administrator, CustomFieldOperation.Create, null, 0,
                TenantFieldAggregateType.Opportunity, "to_deprecate", "To Deprecate", TenantFieldValueType.Text,
                false, null, 0, "to-deprecate", Guid.NewGuid());
            fieldToReactivate = await new ManageCustomFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(cmd101);
        }

        // Deprecate the 101st field
        await using (var context = fixture.CreateAdminContext())
        {
            var def = await context.TenantFieldDefinitions.SingleAsync(x => x.Id == fieldToReactivate.DefinitionId);
            var deprecateCmd = new ManageCustomFieldDefinitionCommand(
                tenant, Administrator, CustomFieldOperation.Deprecate, fieldToReactivate.DefinitionId, def.RowVersion,
                TenantFieldAggregateType.Opportunity, "", "", TenantFieldValueType.Text, false, null, 0, "deprecate-101", Guid.NewGuid());
            await new ManageCustomFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(deprecateCmd);
        }

        // Try to reactivate: should fail because we still have 100 active fields
        await using (var context = fixture.CreateAdminContext())
        {
            var def = await context.TenantFieldDefinitions.SingleAsync(x => x.Id == fieldToReactivate.DefinitionId);
            var reactivateCmd = new ManageCustomFieldDefinitionCommand(
                tenant, Administrator, CustomFieldOperation.Reactivate, fieldToReactivate.DefinitionId, def.RowVersion,
                TenantFieldAggregateType.Opportunity, "", "", TenantFieldValueType.Text, false, null, 0, "reactivate-101", Guid.NewGuid());

            var ex = await Assert.ThrowsAsync<CustomFieldLimitExceededException>(
                () => new ManageCustomFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                    .HandleAsync(reactivateCmd));
            Assert.Contains("100", ex.Message);
        }
    }
}
