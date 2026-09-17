using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Integration;

/// <summary>CRM_Phase1_Test_Coverage_Verification_Report.pdf F-01 (T-01): a persistence
/// round trip through the full PipelineDefinition → PipelineDefinitionVersion → PipelineStage
/// hierarchy was never exercised. Writing this test surfaced a real defect (see the sibling
/// commit fixing PipelineDefinition.AddVersion/PipelineDefinitionVersion.AddStage) rather than
/// merely closing a coverage gap. `Versions`/`Stages` are EF-`Ignore()`d (no cascade-insert
/// navigation, see the Configurations), so each level is explicitly added to its own DbSet and
/// saved before the next level can be built.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class PipelineDefinitionPersistenceTests
{
    private readonly PostgresFixture _fixture;

    public PipelineDefinitionPersistenceTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Definition_version_and_stage_persist_and_reload_with_correct_relationships()
    {
        var tenant = TestData.NextTenant();
        long definitionId, versionId, stageId;

        await using (var context = _fixture.CreateAdminContext())
        {
            var definition = PipelineDefinition.Create(tenant, "Sales Pipeline");
            context.PipelineDefinitions.Add(definition);
            await context.SaveChangesAsync();
            definitionId = definition.Id;

            var version = definition.AddVersion(versionNumber: 1);
            context.PipelineDefinitionVersions.Add(version);
            await context.SaveChangesAsync();
            versionId = version.Id;

            var stage = version.AddStage("Lead", sortOrder: 1);
            context.PipelineStages.Add(stage);
            await context.SaveChangesAsync();
            stageId = stage.Id;
        }

        await using var verification = _fixture.CreateAdminContext();

        var reloadedDefinition = await verification.PipelineDefinitions.AsNoTracking()
            .SingleAsync(d => d.Id == definitionId);
        var reloadedVersion = await verification.PipelineDefinitionVersions.AsNoTracking()
            .SingleAsync(v => v.Id == versionId);
        var reloadedStage = await verification.PipelineStages.AsNoTracking()
            .SingleAsync(s => s.Id == stageId);

        Assert.Equal(tenant, reloadedDefinition.TenantId);
        Assert.Equal(tenant, reloadedVersion.TenantId);
        Assert.Equal(tenant, reloadedStage.TenantId);
        Assert.Equal(definitionId, reloadedVersion.PipelineDefinitionId);
        Assert.Equal(versionId, reloadedStage.PipelineDefinitionVersionId);
        Assert.Equal(1, reloadedVersion.VersionNumber);
        Assert.Equal("Lead", reloadedStage.Name);
        Assert.Equal(1, reloadedStage.SortOrder);
    }
}
