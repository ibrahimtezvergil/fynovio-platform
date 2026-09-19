using Contracts;
using CRM.Application;
using CRM.Domain;
using CRM.Tests.Integration;
using Xunit;

namespace CRM.Tests.Application;

[Collection(nameof(PostgresCollection))]
public sealed class GetPipelineStagesHandlerTests
{
    private readonly PostgresFixture _fixture;

    public GetPipelineStagesHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Returns_every_stage_for_the_requested_version_in_sort_order()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        var definition = PipelineDefinition.Create(tenant, "Sales");
        seed.PipelineDefinitions.Add(definition);
        await seed.SaveChangesAsync();
        var version = definition.AddVersion(1);
        seed.PipelineDefinitionVersions.Add(version);
        await seed.SaveChangesAsync();
        var first = version.AddStage("Bekliyor", 0);
        var second = version.AddStage("Teklif Verildi", 1);
        seed.PipelineStages.AddRange(first, second);
        await seed.SaveChangesAsync();

        await using var context = _fixture.CreateAdminContext();
        var results = await new GetPipelineStagesHandler(context)
            .HandleAsync(new GetPipelineStagesQuery(tenant, version.Id, Guid.NewGuid()));

        Assert.Equal(2, results.Count);
        Assert.Equal("Bekliyor", results[0].Name);
        Assert.True(results[0].IsEntry);
        Assert.Equal("Teklif Verildi", results[1].Name);
    }
}
