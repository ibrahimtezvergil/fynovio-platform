using Contracts;
using SemanticCatalog.Application;
using SemanticCatalog.Domain;
using Xunit;

namespace SemanticCatalog.Tests.Integration;

/// <summary>The reader runs on its own connection inside someone else's request, so it must set the tenant context itself.
/// Under FORCE RLS an unset context returns zero rows with no error — every consumer would read "no definitions" — so these
/// run as the runtime role, never through an admin context (which is how the old outbox dispatcher's no-op went unnoticed).</summary>
[Collection(nameof(PostgresCollection))]
public sealed class SemanticDefinitionReaderTests(PostgresFixture fixture)
{
    private async Task<SemanticDefinitionReader> RuntimeReaderAsync() =>
        new(PostgresFixture.CreateContext(await fixture.RuntimeConnectionStringAsync()));

    [Fact]
    public async Task It_returns_the_tenants_definitions_ordered_and_nothing_of_another_tenant()
    {
        var (tenant, other) = (TestTenants.Next(), TestTenants.Next());
        await using (var admin = fixture.CreateAdminContext())
        {
            admin.FieldDefinitions.AddRange(
                CatalogFieldDefinition.Create(tenant, "crm", "opportunity", "zeta", "Zeta", FieldType.Text, sortOrder: 1),
                CatalogFieldDefinition.Create(tenant, "crm", "opportunity", "alpha", "Alpha", FieldType.Select, config: new FieldConfig(Options: [new FieldOption("a", "A")]), sortOrder: 1),
                CatalogFieldDefinition.Create(tenant, "crm", "opportunity", "first", "First", FieldType.Boolean, isRequired: true, sortOrder: 0),
                CatalogFieldDefinition.Create(other, "crm", "opportunity", "foreign", "Foreign", FieldType.Text));
            await admin.SaveChangesAsync();
        }

        var fields = await (await RuntimeReaderAsync()).ListFieldsAsync(tenant, "crm", "opportunity");

        Assert.Equal(["first", "alpha", "zeta"], fields.Select(f => f.Key));
        Assert.All(fields, f => Assert.Equal(tenant, f.TenantId));
        Assert.True(fields[0].IsRequired);
        Assert.Equal("a", fields[1].Config.Options!.Single().Key);
    }

    [Fact]
    public async Task It_reads_a_definition_by_id_only_inside_its_tenant()
    {
        var (tenant, other) = (TestTenants.Next(), TestTenants.Next());
        CatalogFieldDefinition definition;
        await using (var admin = fixture.CreateAdminContext())
        {
            definition = CatalogFieldDefinition.Create(tenant, "crm", "opportunity", "region", "Region", FieldType.Text);
            admin.FieldDefinitions.Add(definition);
            await admin.SaveChangesAsync();
        }

        var reader = await RuntimeReaderAsync();
        var found = await reader.GetFieldAsync(tenant, definition.Id);
        Assert.Equal("region", found?.Key);
        Assert.Null(await reader.GetFieldAsync(other, definition.Id));
    }

    [Fact]
    public async Task It_includes_deprecated_definitions_so_stored_values_stay_readable()
    {
        var tenant = TestTenants.Next();
        await using (var admin = fixture.CreateAdminContext())
        {
            var old = CatalogFieldDefinition.Create(tenant, "crm", "opportunity", "legacy", "Legacy", FieldType.Text);
            old.Deprecate();
            admin.FieldDefinitions.Add(old);
            await admin.SaveChangesAsync();
        }

        var field = Assert.Single(await (await RuntimeReaderAsync()).ListFieldsAsync(tenant, "crm", "opportunity"));
        Assert.Equal(FieldStatus.Deprecated, field.Status);
        Assert.False(field.IsActive);
    }
}
