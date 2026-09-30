using Contracts;
using CRM.Customization;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CRM.Tests.Integration;

/// <summary>Persistence, constraint enforcement, and RLS coverage for tenant field definitions.
/// Task 2 (Tier-1 Custom Fields): verifies the expanded schema (label, config, status,
/// sort_order, owner_scope, row_version, updated_at) round-trips correctly and all CHECK
/// constraints (including aggregate_type, field_type, field_name, status, owner_scope,
/// sort_order, config object validation) are enforced at the database layer, not just
/// by the domain guard. Tenant isolation tests use the unprivileged runtime role (FF03).</summary>
[Collection(nameof(PostgresCollection))]
public sealed class TenantFieldDefinitionPersistenceTests
{
    private readonly PostgresFixture _fixture;

    public TenantFieldDefinitionPersistenceTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Domain_created_definition_with_all_new_columns_round_trips()
    {
        var tenant = TestData.NextTenant();

        var config = TenantFieldConfig.Empty;
        var definition = TenantFieldDefinition.Create(
            tenant,
            TenantFieldAggregateType.Opportunity,
            "budget_cap",
            "Budget Cap (USD)",
            TenantFieldValueType.Decimal,
            isRequired: true,
            config,
            sortOrder: 50);

        await using (var context = _fixture.CreateAdminContext())
        {
            context.TenantFieldDefinitions.Add(definition);
            await context.SaveChangesAsync();
        }

        await using (var context = _fixture.CreateAdminContext())
        {
            var loaded = await context.TenantFieldDefinitions
                .AsNoTracking()
                .SingleAsync(d => d.TenantId == tenant && d.FieldName == "budget_cap");

            Assert.Equal(tenant, loaded.TenantId);
            Assert.Equal(TenantFieldAggregateType.Opportunity, loaded.AggregateType);
            Assert.Equal("budget_cap", loaded.FieldName);
            Assert.Equal("Budget Cap (USD)", loaded.Label);
            Assert.Equal(TenantFieldValueType.Decimal, loaded.FieldType);
            Assert.True(loaded.IsRequired);
            // Config is validated and enriched with field-type constraints on Create
            Assert.NotNull(loaded.ConfigJson);
            Assert.True(loaded.ConfigJson.Length > 2); // Has validation metadata
            Assert.Equal(TenantFieldStatus.Active, loaded.Status);
            Assert.Equal(50, loaded.SortOrder);
            Assert.Equal(TenantFieldOwnerScope.Tenant, loaded.OwnerScope);
            Assert.Equal(1L, loaded.RowVersion);
            Assert.NotEqual(default(DateTimeOffset), loaded.CreatedAt);
            Assert.NotEqual(default(DateTimeOffset), loaded.UpdatedAt);
        }
    }

    [Fact]
    public async Task Check_constraint_rejects_bad_aggregate_type()
    {
        await using var context = _fixture.CreateAdminContext();
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO crm.tenant_field_definitions
                  (tenant_id, aggregate_type, field_name, field_type, is_required, label, config, status, sort_order, owner_scope, created_at)
                VALUES (1001, 'InvalidType', 'test_field', 'text', false, 'Test', '{{}}'::jsonb, 'Active', 0, 'Tenant', now())
                "));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_tenant_field_definitions_aggregate_type", exception.ConstraintName);
    }

    [Fact]
    public async Task Check_constraint_rejects_bad_field_type()
    {
        await using var context = _fixture.CreateAdminContext();
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO crm.tenant_field_definitions
                  (tenant_id, aggregate_type, field_name, field_type, is_required, label, config, status, sort_order, owner_scope, created_at)
                VALUES (1002, 'Opportunity', 'test_field', 'invalid_type', false, 'Test', '{{}}'::jsonb, 'Active', 0, 'Tenant', now())
                "));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_tenant_field_definitions_field_type", exception.ConstraintName);
    }

    [Fact]
    public async Task Check_constraint_rejects_bad_field_name_format()
    {
        await using var context = _fixture.CreateAdminContext();

        // Starting with uppercase
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO crm.tenant_field_definitions
                  (tenant_id, aggregate_type, field_name, field_type, is_required, label, config, status, sort_order, owner_scope, created_at)
                VALUES (1003, 'Opportunity', 'TestField', 'text', false, 'Test', '{{}}'::jsonb, 'Active', 0, 'Tenant', now())
                "));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_tenant_field_definitions_field_name", exception.ConstraintName);
    }

    [Fact]
    public async Task Check_constraint_rejects_field_name_too_short()
    {
        await using var context = _fixture.CreateAdminContext();

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO crm.tenant_field_definitions
                  (tenant_id, aggregate_type, field_name, field_type, is_required, label, config, status, sort_order, owner_scope, created_at)
                VALUES (1004, 'Opportunity', 'a', 'text', false, 'Test', '{{}}'::jsonb, 'Active', 0, 'Tenant', now())
                "));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_tenant_field_definitions_field_name", exception.ConstraintName);
    }

    [Fact]
    public async Task Check_constraint_rejects_bad_status()
    {
        await using var context = _fixture.CreateAdminContext();
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO crm.tenant_field_definitions
                  (tenant_id, aggregate_type, field_name, field_type, is_required, label, config, status, sort_order, owner_scope, created_at)
                VALUES (1005, 'Opportunity', 'test_field', 'text', false, 'Test', '{{}}'::jsonb, 'Invalid', 0, 'Tenant', now())
                "));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_tenant_field_definitions_status", exception.ConstraintName);
    }

    [Fact]
    public async Task Check_constraint_rejects_owner_scope_not_tenant()
    {
        await using var context = _fixture.CreateAdminContext();
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO crm.tenant_field_definitions
                  (tenant_id, aggregate_type, field_name, field_type, is_required, label, config, status, sort_order, owner_scope, created_at)
                VALUES (1006, 'Opportunity', 'test_field', 'text', false, 'Test', '{{}}'::jsonb, 'Active', 0, 'Other', now())
                "));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_tenant_field_definitions_owner_scope", exception.ConstraintName);
    }

    [Fact]
    public async Task Check_constraint_rejects_sort_order_below_zero()
    {
        await using var context = _fixture.CreateAdminContext();
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO crm.tenant_field_definitions
                  (tenant_id, aggregate_type, field_name, field_type, is_required, label, config, status, sort_order, owner_scope, created_at)
                VALUES (1007, 'Opportunity', 'test_field', 'text', false, 'Test', '{{}}'::jsonb, 'Active', -1, 'Tenant', now())
                "));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_tenant_field_definitions_sort_order", exception.ConstraintName);
    }

    [Fact]
    public async Task Check_constraint_rejects_sort_order_above_max()
    {
        await using var context = _fixture.CreateAdminContext();
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO crm.tenant_field_definitions
                  (tenant_id, aggregate_type, field_name, field_type, is_required, label, config, status, sort_order, owner_scope, created_at)
                VALUES (1008, 'Opportunity', 'test_field', 'text', false, 'Test', '{{}}'::jsonb, 'Active', 10001, 'Tenant', now())
                "));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_tenant_field_definitions_sort_order", exception.ConstraintName);
    }

    [Fact]
    public async Task Check_constraint_rejects_non_object_config()
    {
        await using var context = _fixture.CreateAdminContext();

        // Array instead of object
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO crm.tenant_field_definitions
                  (tenant_id, aggregate_type, field_name, field_type, is_required, label, config, status, sort_order, owner_scope, created_at)
                VALUES (1009, 'Opportunity', 'test_field', 'text', false, 'Test', '[]'::jsonb, 'Active', 0, 'Tenant', now())
                "));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_tenant_field_definitions_config_object", exception.ConstraintName);
    }

    [Fact]
    public async Task Unique_index_rejects_duplicate_field_in_same_tenant_and_aggregate_type()
    {
        var tenant = TestData.NextTenant();

        await using (var context = _fixture.CreateAdminContext())
        {
            var first = TenantFieldDefinition.Create(
                tenant,
                TenantFieldAggregateType.Opportunity,
                "test_field",
                "Test Field",
                TenantFieldValueType.Text);
            context.TenantFieldDefinitions.Add(first);
            await context.SaveChangesAsync();
        }

        await using (var context = _fixture.CreateAdminContext())
        {
            var second = TenantFieldDefinition.Create(
                tenant,
                TenantFieldAggregateType.Opportunity,
                "test_field",
                "Another Label",
                TenantFieldValueType.Text);
            context.TenantFieldDefinitions.Add(second);
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Same_field_name_in_different_aggregate_types_within_same_tenant_is_allowed()
    {
        var tenant = TestData.NextTenant();

        await using (var context = _fixture.CreateAdminContext())
        {
            var opportunityField = TenantFieldDefinition.Create(
                tenant,
                TenantFieldAggregateType.Opportunity,
                "contact_method",
                "Contact Method",
                TenantFieldValueType.Text);
            var partyField = TenantFieldDefinition.Create(
                tenant,
                TenantFieldAggregateType.Party,
                "contact_method",
                "Contact Method",
                TenantFieldValueType.Text);
            context.TenantFieldDefinitions.Add(opportunityField);
            context.TenantFieldDefinitions.Add(partyField);
            await context.SaveChangesAsync();
        }

        await using var verify = _fixture.CreateAdminContext();
        var count = await verify.TenantFieldDefinitions
            .Where(d => d.TenantId == tenant && d.FieldName == "contact_method")
            .CountAsync();
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Same_field_name_in_different_tenants_is_allowed()
    {
        var tenantA = TestData.NextTenant();
        var tenantB = TestData.NextTenant();

        await using (var context = _fixture.CreateAdminContext())
        {
            var fieldA = TenantFieldDefinition.Create(
                tenantA,
                TenantFieldAggregateType.Opportunity,
                "priority",
                "Priority",
                TenantFieldValueType.Text);
            var fieldB = TenantFieldDefinition.Create(
                tenantB,
                TenantFieldAggregateType.Opportunity,
                "priority",
                "Priority",
                TenantFieldValueType.Text);
            context.TenantFieldDefinitions.Add(fieldA);
            context.TenantFieldDefinitions.Add(fieldB);
            await context.SaveChangesAsync();
        }

        await using var verify = _fixture.CreateAdminContext();
        var countA = await verify.TenantFieldDefinitions
            .Where(d => d.TenantId == tenantA && d.FieldName == "priority")
            .CountAsync();
        var countB = await verify.TenantFieldDefinitions
            .Where(d => d.TenantId == tenantB && d.FieldName == "priority")
            .CountAsync();
        Assert.Equal(1, countA);
        Assert.Equal(1, countB);
    }

    [Fact]
    public async Task Runtime_role_cannot_read_another_tenants_field_definitions()
    {
        var (_, fieldIdA) = await SeedDefinitionAsync();
        var (tenantB, fieldIdB) = await SeedDefinitionAsync();

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenantB);

        var visible = await context.TenantFieldDefinitions.AsNoTracking().ToListAsync();

        Assert.Contains(visible, f => f.Id == fieldIdB);
        Assert.DoesNotContain(visible, f => f.Id == fieldIdA);
        Assert.All(visible, f => Assert.Equal(tenantB, f.TenantId));
    }

    [Fact]
    public async Task Runtime_role_cannot_read_a_guessed_id_from_another_tenant()
    {
        var (_, fieldIdA) = await SeedDefinitionAsync();
        var (tenantB, _) = await SeedDefinitionAsync();

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenantB);

        var guessed = await context.TenantFieldDefinitions
            .AsNoTracking()
            .SingleOrDefaultAsync(f => f.Id == fieldIdA);

        Assert.Null(guessed);
    }

    [Fact]
    public async Task Runtime_role_cannot_write_a_field_definition_for_another_tenant()
    {
        var (tenantA, _) = await SeedDefinitionAsync();
        var tenantB = TestData.NextTenant();

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenantB);

        var foreignDef = TenantFieldDefinition.Create(
            tenantA,
            TenantFieldAggregateType.Opportunity,
            "injected_field",
            "Injected",
            TenantFieldValueType.Text);
        context.TenantFieldDefinitions.Add(foreignDef);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, postgresException.SqlState);
    }

    private async Task<(TenantId TenantId, long FieldId)> SeedDefinitionAsync()
    {
        var tenant = TestData.NextTenant();
        var counter = Interlocked.Increment(ref _seedCounter);

        await using var context = _fixture.CreateAdminContext();
        var definition = TenantFieldDefinition.Create(
            tenant,
            TenantFieldAggregateType.Opportunity,
            $"field_{counter}",
            "Test Field",
            TenantFieldValueType.Text);
        context.TenantFieldDefinitions.Add(definition);
        await context.SaveChangesAsync();

        return (tenant, definition.Id);
    }

    private static long _seedCounter;
}
