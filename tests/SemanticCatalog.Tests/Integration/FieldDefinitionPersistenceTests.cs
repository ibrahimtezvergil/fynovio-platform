using Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SemanticCatalog.Domain;
using SemanticCatalog.Persistence;
using Xunit;

namespace SemanticCatalog.Tests.Integration;

/// <summary>Persistence, constraint enforcement and RLS for field definitions: the CHECK constraints hold at the database
/// layer, not just in the domain guard, and tenant isolation is proven as the unprivileged runtime role (FF03) — the
/// container's superuser would bypass RLS and prove nothing.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class FieldDefinitionPersistenceTests(PostgresFixture fixture)
{
    private static long _seedCounter;

    [Fact]
    public async Task A_definition_round_trips_with_every_column()
    {
        var tenant = TestTenants.Next();
        var definition = CatalogFieldDefinition.Create(tenant, "crm", "opportunity", "budget_cap", "Budget Cap (USD)", FieldType.Decimal,
            isRequired: true, sortOrder: 50);

        await using (var context = fixture.CreateAdminContext())
        {
            context.FieldDefinitions.Add(definition);
            await context.SaveChangesAsync();
        }

        await using var verify = fixture.CreateAdminContext();
        var loaded = await verify.FieldDefinitions.AsNoTracking().SingleAsync(d => d.TenantId == tenant && d.Key == "budget_cap");
        Assert.Equal(("crm", "opportunity"), (loaded.OwnerContext, loaded.ObjectType));
        Assert.Equal("Budget Cap (USD)", loaded.Label);
        Assert.Equal(FieldType.Decimal, loaded.Type);
        Assert.True(loaded.IsRequired);
        Assert.Equal(2, loaded.Config.Scale);
        Assert.Equal(FieldStatus.Active, loaded.Status);
        Assert.Equal(50, loaded.SortOrder);
        Assert.Equal(1L, loaded.RowVersion);
        Assert.NotEqual(default, loaded.CreatedAt);
    }

    [Theory]
    [InlineData("'sales', 'opportunity', 'test_field', 'text', 'Active', 0, '{}'", "ck_field_definitions_owner")]
    [InlineData("'crm', 'party', 'test_field', 'text', 'Active', 0, '{}'", "ck_field_definitions_owner")]
    [InlineData("'crm', 'opportunity', 'test_field', 'money', 'Active', 0, '{}'", "ck_field_definitions_field_type")]
    [InlineData("'crm', 'opportunity', 'TestField', 'text', 'Active', 0, '{}'", "ck_field_definitions_key")]
    [InlineData("'crm', 'opportunity', 'a', 'text', 'Active', 0, '{}'", "ck_field_definitions_key")]
    [InlineData("'crm', 'opportunity', 'test_field', 'text', 'Gone', 0, '{}'", "ck_field_definitions_status")]
    [InlineData("'crm', 'opportunity', 'test_field', 'text', 'Active', -1, '{}'", "ck_field_definitions_sort_order")]
    [InlineData("'crm', 'opportunity', 'test_field', 'text', 'Active', 10001, '{}'", "ck_field_definitions_sort_order")]
    [InlineData("'crm', 'opportunity', 'test_field', 'text', 'Active', 0, '[]'", "ck_field_definitions_config_object")]
    public async Task The_database_rejects_what_the_domain_would_never_write(string values, string constraint)
    {
        await using var connection = new NpgsqlConnection(fixture.AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"""
            INSERT INTO semantic.field_definitions (tenant_id, owner_context, object_type, key, field_type, status, sort_order, config, label, is_required, row_version, created_at)
            SELECT 1001, v.owner_context, v.object_type, v.key, v.field_type, v.status, v.sort_order, v.config::jsonb, 'Test', false, 1, now()
            FROM (VALUES ({values})) AS v(owner_context, object_type, key, field_type, status, sort_order, config)
            """, connection);
        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal(constraint, exception.ConstraintName);
    }

    [Fact]
    public async Task A_key_is_unique_per_tenant_and_owner_but_free_in_another_tenant()
    {
        var (tenantA, tenantB) = (TestTenants.Next(), TestTenants.Next());
        await using (var context = fixture.CreateAdminContext())
        {
            context.FieldDefinitions.Add(CatalogFieldDefinition.Create(tenantA, "crm", "opportunity", "region", "Region", FieldType.Text));
            context.FieldDefinitions.Add(CatalogFieldDefinition.Create(tenantB, "crm", "opportunity", "region", "Region", FieldType.Text));
            await context.SaveChangesAsync();
        }

        await using var duplicate = fixture.CreateAdminContext();
        duplicate.FieldDefinitions.Add(CatalogFieldDefinition.Create(tenantA, "crm", "opportunity", "region", "Another label", FieldType.Text));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
        Assert.Equal("ix_field_definitions_tenant_id_owner_context_object_type_key", Assert.IsType<PostgresException>(exception.InnerException).ConstraintName);
    }

    [Fact]
    public async Task The_runtime_role_sees_nothing_without_a_tenant_context_and_only_its_own_with_one()
    {
        var (_, idA) = await SeedAsync();
        var (tenantB, idB) = await SeedAsync();

        await using var context = PostgresFixture.CreateContext(await fixture.RuntimeConnectionStringAsync());
        Assert.Empty(await context.FieldDefinitions.AsNoTracking().ToListAsync());

        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenantB);
        var visible = await context.FieldDefinitions.AsNoTracking().ToListAsync();

        Assert.Contains(visible, f => f.Id == idB);
        Assert.DoesNotContain(visible, f => f.Id == idA);
        Assert.All(visible, f => Assert.Equal(tenantB, f.TenantId));
        Assert.Null(await context.FieldDefinitions.AsNoTracking().SingleOrDefaultAsync(f => f.Id == idA));
    }

    [Fact]
    public async Task The_runtime_role_cannot_write_a_definition_for_another_tenant()
    {
        var (tenantA, _) = await SeedAsync();
        var tenantB = TestTenants.Next();

        await using var context = PostgresFixture.CreateContext(await fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenantB);
        context.FieldDefinitions.Add(CatalogFieldDefinition.Create(tenantA, "crm", "opportunity", "injected_field", "Injected", FieldType.Text));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    [Theory]
    [InlineData("semantic.idempotency_records")]
    [InlineData("semantic.evidence_records")]
    [InlineData("semantic.outbox_messages")]
    public async Task Every_catalog_table_is_row_level_secured_and_forced(string table)
    {
        await using var connection = new NpgsqlConnection(fixture.AdminConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT relrowsecurity AND relforcerowsecurity FROM pg_class WHERE oid = '{table}'::regclass";
        Assert.True((bool)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Evidence_is_append_only_for_the_runtime_role()
    {
        var tenant = TestTenants.Next();
        await using var context = PostgresFixture.CreateContext(await fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenant);

        var denied = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlRawAsync("UPDATE semantic.evidence_records SET action = 'x'"));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
    }

    private async Task<(TenantId TenantId, long FieldId)> SeedAsync()
    {
        var tenant = TestTenants.Next();
        await using var context = fixture.CreateAdminContext();
        var definition = CatalogFieldDefinition.Create(tenant, "crm", "opportunity", $"field_{Interlocked.Increment(ref _seedCounter)}", "Test Field", FieldType.Text);
        context.FieldDefinitions.Add(definition);
        await context.SaveChangesAsync();
        return (tenant, definition.Id);
    }
}
