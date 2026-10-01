using System.Text.Json;
using Contracts;
using SemanticCatalog.Application;
using SemanticCatalog.Domain;
using SemanticCatalog.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SemanticCatalog.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class FieldDefinitionHandlerTests(PostgresFixture fixture)
{
    private static readonly PrincipalRef Administrator = new("https://identity.test", "crm-settings-admin");

    [Fact]
    public async Task Create_is_idempotent_and_commits_outbox_and_evidence()
    {
        var tenant = TestTenants.Next();
        var command = new ManageFieldDefinitionCommand(
            tenant,
            Administrator,
            FieldOperation.Create,
            null,
            0,
            "crm", "opportunity",
            "business_unit",
            "Business Unit",
            FieldType.Text,
            false,
            null,
            0,
            "custom-field-create-once",
            Guid.NewGuid());

        ManageFieldDefinitionResult first;
        await using (var context = fixture.CreateAdminContext())
            first = await new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(command);

        Assert.True(first.DefinitionId > 0);
        Assert.Equal(1, first.RowVersion);
        Assert.False(first.Replayed);

        await using (var context = fixture.CreateAdminContext())
        {
            var replay = await new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(command with { CorrelationId = Guid.NewGuid() });

            Assert.True(replay.Replayed);
            Assert.Equal(first.DefinitionId, replay.DefinitionId);
            Assert.Single(await context.FieldDefinitions
                .Where(x => x.TenantId == tenant && x.Key == "business_unit")
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
        var tenant = TestTenants.Next();
        var idempotencyKey = "duplicate-key";
        var correlationId = Guid.NewGuid();

        var command1 = new ManageFieldDefinitionCommand(tenant, Administrator, FieldOperation.Create, null, 0,
            "crm", "opportunity", "field_one", "Field One", FieldType.Text, false, null, 0, idempotencyKey, correlationId);

        await using (var context = fixture.CreateAdminContext())
            await new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(command1);

        var command2 = new ManageFieldDefinitionCommand(tenant, Administrator, FieldOperation.Create, null, 0,
            "crm", "opportunity", "field_two", "Field Two", FieldType.Text, false, null, 0, idempotencyKey, Guid.NewGuid());

        await using (var context = fixture.CreateAdminContext())
        {
            var ex = await Assert.ThrowsAsync<IdempotencyKeyReusedException>(
                () => new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                    .HandleAsync(command2));
            Assert.Contains("duplicate-key", ex.Message);
        }
    }

    [Fact]
    public async Task Party_definitions_are_refused_until_OD_6_changes()
    {
        var tenant = TestTenants.Next();
        var command = new ManageFieldDefinitionCommand(
            tenant,
            Administrator,
            FieldOperation.Create,
            null,
            0,
            "crm",
            "party",
            "party_field",
            "Party Field",
            FieldType.Text,
            false,
            null,
            0,
            "party-refused",
            Guid.NewGuid());

        await using (var context = fixture.CreateAdminContext())
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(
                () => new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                    .HandleAsync(command));
            Assert.Contains("crm/party", ex.Message);
        }
    }

    [Fact]
    public async Task Duplicate_field_key_throws_conflict()
    {
        var tenant = TestTenants.Next();

        var command1 = new ManageFieldDefinitionCommand(tenant, Administrator, FieldOperation.Create, null, 0,
            "crm", "opportunity", "region", "Region", FieldType.Text, false, null, 0, "first-field", Guid.NewGuid());

        await using (var context = fixture.CreateAdminContext())
            await new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(command1);

        var command2 = new ManageFieldDefinitionCommand(tenant, Administrator, FieldOperation.Create, null, 0,
            "crm", "opportunity", "region", "Region (different label)", FieldType.Text, false, null, 0, "second-field", Guid.NewGuid());

        await using (var context = fixture.CreateAdminContext())
        {
            var ex = await Assert.ThrowsAsync<FieldKeyConflictException>(
                () => new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                    .HandleAsync(command2));
            Assert.Contains("region", ex.Message);
        }
    }

    [Fact]
    public async Task Creating_the_101st_active_field_fails()
    {
        var tenant = TestTenants.Next();

        await using (var context = fixture.CreateAdminContext())
        {
            for (int i = 0; i < 100; i++)
            {
                var cmd = new ManageFieldDefinitionCommand(
                    tenant, Administrator, FieldOperation.Create, null, 0,
                    "crm", "opportunity", $"field_{i:D3}", $"Field {i}", FieldType.Text,
                    false, null, 0, $"idempotency-{i}", Guid.NewGuid());
                await new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                    .HandleAsync(cmd);
            }
        }

        var commandFor101 = new ManageFieldDefinitionCommand(
            tenant, Administrator, FieldOperation.Create, null, 0,
            "crm", "opportunity", "field_100", "Field 100", FieldType.Text,
            false, null, 0, "idempotency-100", Guid.NewGuid());

        await using (var context = fixture.CreateAdminContext())
        {
            var ex = await Assert.ThrowsAsync<FieldLimitExceededException>(
                () => new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                    .HandleAsync(commandFor101));
            Assert.Contains("100", ex.Message);
        }
    }

    [Fact]
    public async Task Update_with_stale_row_version_fails()
    {
        var tenant = TestTenants.Next();

        ManageFieldDefinitionResult created;
        await using (var context = fixture.CreateAdminContext())
        {
            var createCmd = new ManageFieldDefinitionCommand(tenant, Administrator, FieldOperation.Create, null, 0,
                "crm", "opportunity", "status_field", "Status", FieldType.Select,
                false,
                new FieldConfigInput(
                    [new FieldOptionInput("active", "Active"), new FieldOptionInput("inactive", "Inactive")],
                    null, null, null, null),
                0, "create-status", Guid.NewGuid());
            created = await new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(createCmd);
        }

        var updateCmd = new ManageFieldDefinitionCommand(
            tenant, Administrator, FieldOperation.Update, created.DefinitionId, 0, // Wrong version
            "crm", "opportunity", "status_field", "Status (Updated)", FieldType.Select,
            false,
            new FieldConfigInput(
                [new FieldOptionInput("active", "Active"), new FieldOptionInput("inactive", "Inactive")],
                null, null, null, null),
            0, "update-status", Guid.NewGuid());

        await using (var context = fixture.CreateAdminContext())
        {
            var ex = await Assert.ThrowsAsync<CatalogConcurrencyConflictException>(
                () => new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                    .HandleAsync(updateCmd));
        }
    }

    [Fact]
    public async Task Deprecate_and_then_reactivate()
    {
        var tenant = TestTenants.Next();

        ManageFieldDefinitionResult created;
        await using (var context = fixture.CreateAdminContext())
        {
            var cmd = new ManageFieldDefinitionCommand(tenant, Administrator, FieldOperation.Create, null, 0,
                "crm", "opportunity", "temp_field", "Temporary Field", FieldType.Text, false, null, 0, "temp-field", Guid.NewGuid());
            created = await new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(cmd);
        }

        await using (var context = fixture.CreateAdminContext())
        {
            var deprecateCmd = new ManageFieldDefinitionCommand(
                tenant, Administrator, FieldOperation.Deprecate, created.DefinitionId, created.RowVersion,
                "crm", "opportunity", "", "", FieldType.Text, false, null, 0, "deprecate-temp", Guid.NewGuid());
            var deprecated = await new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(deprecateCmd);

            Assert.Equal(2, deprecated.RowVersion);
            var def = await context.FieldDefinitions.SingleAsync(x => x.Id == created.DefinitionId);
            Assert.Equal(FieldStatus.Deprecated, def.Status);
        }

        await using (var context = fixture.CreateAdminContext())
        {
            var def = await context.FieldDefinitions.SingleAsync(x => x.Id == created.DefinitionId);
            var reactivateCmd = new ManageFieldDefinitionCommand(
                tenant, Administrator, FieldOperation.Reactivate, created.DefinitionId, def.RowVersion,
                "crm", "opportunity", "", "", FieldType.Text, false, null, 0, "reactivate-temp", Guid.NewGuid());
            var reactivated = await new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(reactivateCmd);

            Assert.Equal(3, reactivated.RowVersion);
            var reactivatedDef = await context.FieldDefinitions.SingleAsync(x => x.Id == created.DefinitionId);
            Assert.Equal(FieldStatus.Active, reactivatedDef.Status);
        }
    }

    [Fact]
    public async Task List_returns_fields_ordered_by_sort_order_then_name()
    {
        var tenant = TestTenants.Next();

        await using (var context = fixture.CreateAdminContext())
        {
            for (int i = 0; i < 3; i++)
            {
                var cmd = new ManageFieldDefinitionCommand(
                    tenant, Administrator, FieldOperation.Create, null, 0,
                    "crm", "opportunity", $"field_{i}", $"Field {i}", FieldType.Text,
                    false, null, i % 2 == 0 ? 10 : 5, $"list-test-{i}", Guid.NewGuid());
                await new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                    .HandleAsync(cmd);
            }
        }

        await using (var context = fixture.CreateAdminContext())
        {
            var query = new ListFieldDefinitionsQuery(
                tenant, Administrator, "crm", "opportunity", Guid.NewGuid());
            var list = await new ListFieldDefinitionsHandler(context, StubAuthorizer.AlwaysAllow)
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
        var tenant = TestTenants.Next();

        await using (var context = fixture.CreateAdminContext())
        {
            var cmd = new ManageFieldDefinitionCommand(
                tenant, Administrator, FieldOperation.Create, null, 0,
                "crm", "opportunity", "test_field", "Test Field", FieldType.Text,
                false, null, 0, "list-auth-test", Guid.NewGuid());
            await new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(cmd);
        }

        await using (var context = fixture.CreateAdminContext())
        {
            var query = new ListFieldDefinitionsQuery(
                tenant, Administrator, "crm", "opportunity", Guid.NewGuid());
            var ex = await Assert.ThrowsAsync<CatalogAuthorizationDeniedException>(
                () => new ListFieldDefinitionsHandler(context, StubAuthorizer.AlwaysDeny)
                    .HandleAsync(query));
        }
    }

    [Fact]
    public async Task Manage_is_gated_by_settings_update_authorization()
    {
        var tenant = TestTenants.Next();
        var command = new ManageFieldDefinitionCommand(
            tenant, Administrator, FieldOperation.Create, null, 0,
            "crm", "opportunity", "secure_field", "Secure Field", FieldType.Text,
            false, null, 0, "secure-field", Guid.NewGuid());

        await using (var context = fixture.CreateAdminContext())
        {
            var ex = await Assert.ThrowsAsync<CatalogAuthorizationDeniedException>(
                () => new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysDeny)
                    .HandleAsync(command));
        }
    }

    [Fact]
    public async Task Reactivating_the_101st_field_after_deprecation_fails()
    {
        var tenant = TestTenants.Next();

        ManageFieldDefinitionResult fieldToReactivate;
        await using (var context = fixture.CreateAdminContext())
        {
            // 99 active fields plus the one we deprecate make 100
            for (int i = 0; i < 99; i++)
            {
                var cmd = new ManageFieldDefinitionCommand(
                    tenant, Administrator, FieldOperation.Create, null, 0,
                    "crm", "opportunity", $"active_{i:D3}", $"Active {i}", FieldType.Text,
                    false, null, 0, $"active-{i}", Guid.NewGuid());
                await new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                    .HandleAsync(cmd);
            }

            // Create one more that we'll deprecate
            var cmd101 = new ManageFieldDefinitionCommand(
                tenant, Administrator, FieldOperation.Create, null, 0,
                "crm", "opportunity", "to_deprecate", "To Deprecate", FieldType.Text,
                false, null, 0, "to-deprecate", Guid.NewGuid());
            fieldToReactivate = await new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(cmd101);
        }

        // Deprecate the 101st field
        await using (var context = fixture.CreateAdminContext())
        {
            var def = await context.FieldDefinitions.SingleAsync(x => x.Id == fieldToReactivate.DefinitionId);
            var deprecateCmd = new ManageFieldDefinitionCommand(
                tenant, Administrator, FieldOperation.Deprecate, fieldToReactivate.DefinitionId, def.RowVersion,
                "crm", "opportunity", "", "", FieldType.Text, false, null, 0, "deprecate-101", Guid.NewGuid());
            await new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                .HandleAsync(deprecateCmd);
        }

        // Fill the freed slot so 100 fields are active again
        await using (var context = fixture.CreateAdminContext())
            await new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(new ManageFieldDefinitionCommand(
                tenant, Administrator, FieldOperation.Create, null, 0,
                "crm", "opportunity", "active_099", "Active 99", FieldType.Text,
                false, null, 0, "active-99", Guid.NewGuid()));

        // Reactivating would make it 101
        await using (var context = fixture.CreateAdminContext())
        {
            var def = await context.FieldDefinitions.SingleAsync(x => x.Id == fieldToReactivate.DefinitionId);
            var reactivateCmd = new ManageFieldDefinitionCommand(
                tenant, Administrator, FieldOperation.Reactivate, fieldToReactivate.DefinitionId, def.RowVersion,
                "crm", "opportunity", "", "", FieldType.Text, false, null, 0, "reactivate-101", Guid.NewGuid());

            var ex = await Assert.ThrowsAsync<FieldLimitExceededException>(
                () => new ManageFieldDefinitionHandler(context, StubAuthorizer.AlwaysAllow)
                    .HandleAsync(reactivateCmd));
            Assert.Contains("100", ex.Message);
        }
    }
}
