using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CRM.Tests.Integration;

/// <summary>CRM_Phase1_Test_Coverage_Verification_Report.pdf F-03 + F-05: proves the DB itself
/// — not the domain guard — enforces tenant-safe referential integrity and the four declared
/// unique indexes on the pipeline tables. Admin (superuser) connection is deliberate, same as
/// Access.Tests' AccessConstraintTests: FK/unique-index checks stay in force for superusers,
/// unlike RLS (see PipelineRlsTests for that side).</summary>
[Collection(nameof(PostgresCollection))]
public sealed class PipelineConstraintTests
{
    private readonly PostgresFixture _fixture;

    public PipelineConstraintTests(PostgresFixture fixture) => _fixture = fixture;

    /// <summary>The public domain API can never construct this mismatch — AddVersion always
    /// stamps its own TenantId — so this goes straight at the table with raw SQL, the way a
    /// migration or a future direct-SQL integration would.</summary>
    [Fact]
    public async Task Version_referencing_a_foreign_tenants_definition_violates_composite_fk()
    {
        var (_, definitionA) = await SeedDefinitionAsync();
        var tenantB = TestData.NextTenant();

        await using var context = _fixture.CreateAdminContext();
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO crm.pipeline_definition_versions (tenant_id, pipeline_definition_id, version_number, created_at)
                VALUES ({tenantB.Value}, {definitionA}, 1, now())
                """));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
    }

    [Fact]
    public async Task Stage_referencing_a_foreign_tenants_version_violates_composite_fk()
    {
        var (_, versionA) = await SeedVersionAsync();
        var tenantB = TestData.NextTenant();

        await using var context = _fixture.CreateAdminContext();
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO crm.pipeline_stages (tenant_id, pipeline_definition_version_id, name, sort_order, created_at)
                VALUES ({tenantB.Value}, {versionA}, 'Lead', 1, now())
                """));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
    }

    [Fact]
    public async Task Duplicate_pipeline_name_in_the_same_tenant_is_rejected_at_the_db()
    {
        var tenant = TestData.NextTenant();

        await using (var first = _fixture.CreateAdminContext())
        {
            first.PipelineDefinitions.Add(PipelineDefinition.Create(tenant, "Sales Pipeline"));
            await first.SaveChangesAsync();
        }

        await using var second = _fixture.CreateAdminContext();
        second.PipelineDefinitions.Add(PipelineDefinition.Create(tenant, "Sales Pipeline"));
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Same_pipeline_name_in_different_tenants_is_allowed()
    {
        var tenantA = TestData.NextTenant();
        var tenantB = TestData.NextTenant();

        await using (var first = _fixture.CreateAdminContext())
        {
            first.PipelineDefinitions.Add(PipelineDefinition.Create(tenantA, "Sales Pipeline"));
            await first.SaveChangesAsync();
        }

        await using var second = _fixture.CreateAdminContext();
        second.PipelineDefinitions.Add(PipelineDefinition.Create(tenantB, "Sales Pipeline"));
        await second.SaveChangesAsync();
    }

    /// <summary>PipelineDefinition.AddVersion only rejects a duplicate version number within
    /// one already-loaded aggregate's in-memory `_versions` list, which is never populated on
    /// reload (`Versions` is EF-`Ignore()`d). Reloading the same saved definition through a
    /// second context therefore bypasses the domain guard exactly the way two concurrent
    /// requests would, proving the DB's own unique index is a real, independent backstop.</summary>
    [Fact]
    public async Task Duplicate_version_number_on_the_same_definition_is_rejected_at_the_db()
    {
        var (tenant, definitionId) = await SeedDefinitionAsync();

        await using (var first = _fixture.CreateAdminContext())
        {
            var definition = await first.PipelineDefinitions.SingleAsync(d => d.Id == definitionId);
            first.PipelineDefinitionVersions.Add(definition.AddVersion(versionNumber: 1));
            await first.SaveChangesAsync();
        }

        await using var second = _fixture.CreateAdminContext();
        var reloaded = await second.PipelineDefinitions.SingleAsync(d => d.Id == definitionId);
        second.PipelineDefinitionVersions.Add(reloaded.AddVersion(versionNumber: 1));
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Duplicate_stage_name_on_the_same_version_is_rejected_at_the_db()
    {
        var (tenant, versionId) = await SeedVersionAsync();
        _ = tenant;

        await using (var first = _fixture.CreateAdminContext())
        {
            var version = await first.PipelineDefinitionVersions.SingleAsync(v => v.Id == versionId);
            first.PipelineStages.Add(version.AddStage("Lead", sortOrder: 1));
            await first.SaveChangesAsync();
        }

        await using var second = _fixture.CreateAdminContext();
        var reloaded = await second.PipelineDefinitionVersions.SingleAsync(v => v.Id == versionId);
        second.PipelineStages.Add(reloaded.AddStage("Lead", sortOrder: 2));
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Duplicate_sort_order_on_the_same_version_is_rejected_at_the_db()
    {
        var (tenant, versionId) = await SeedVersionAsync();
        _ = tenant;

        await using (var first = _fixture.CreateAdminContext())
        {
            var version = await first.PipelineDefinitionVersions.SingleAsync(v => v.Id == versionId);
            first.PipelineStages.Add(version.AddStage("Lead", sortOrder: 1));
            await first.SaveChangesAsync();
        }

        await using var second = _fixture.CreateAdminContext();
        var reloaded = await second.PipelineDefinitionVersions.SingleAsync(v => v.Id == versionId);
        second.PipelineStages.Add(reloaded.AddStage("Contacted", sortOrder: 1));
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Two_entry_stages_in_the_same_version_violate_the_partial_unique_index()
    {
        var (tenant, versionId) = await SeedVersionAsync();
        _ = tenant;

        await using (var seed = _fixture.CreateAdminContext())
        {
            var seedVersion = await seed.PipelineDefinitionVersions.SingleAsync(v => v.Id == versionId);
            seed.PipelineStages.Add(seedVersion.AddStage("Bekliyor", sortOrder: 0));
            await seed.SaveChangesAsync();
        }

        await using var context = _fixture.CreateAdminContext();
        var version = await context.PipelineDefinitionVersions.SingleAsync(v => v.Id == versionId);

        var first = await context.PipelineStages.SingleAsync(s => s.PipelineDefinitionVersionId == versionId);
        Assert.True(first.IsEntry);

        // AddStage only makes the *first* stage entry by default; force a second entry row
        // directly to prove the database — not just the aggregate's MarkEntry — rejects it.
        var second = version.AddStage("Teklif Verildi", sortOrder: 1);
        typeof(PipelineStage).GetMethod("SetEntry", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(second, [true]);
        context.PipelineStages.Add(second);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private async Task<(Contracts.TenantId TenantId, long DefinitionId)> SeedDefinitionAsync()
    {
        var tenant = TestData.NextTenant();
        await using var context = _fixture.CreateAdminContext();
        var definition = PipelineDefinition.Create(tenant, $"Pipeline {Guid.NewGuid()}");
        context.PipelineDefinitions.Add(definition);
        await context.SaveChangesAsync();
        return (tenant, definition.Id);
    }

    private async Task<(Contracts.TenantId TenantId, long VersionId)> SeedVersionAsync()
    {
        var (tenant, definitionId) = await SeedDefinitionAsync();
        await using var context = _fixture.CreateAdminContext();
        var definition = await context.PipelineDefinitions.SingleAsync(d => d.Id == definitionId);
        var version = definition.AddVersion(versionNumber: 1);
        context.PipelineDefinitionVersions.Add(version);
        await context.SaveChangesAsync();
        return (tenant, version.Id);
    }
}
