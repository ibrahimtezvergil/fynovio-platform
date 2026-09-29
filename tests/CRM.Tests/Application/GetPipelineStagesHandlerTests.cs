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
        var results = await new GetPipelineStagesHandler(context, StubAuthorizer.AlwaysAllow)
            .HandleAsync(new GetPipelineStagesQuery(tenant, version.Id, TestData.Seller, Guid.NewGuid()));

        Assert.Equal(2, results.Count);
        Assert.Equal("Bekliyor", results[0].Name);
        Assert.True(results[0].IsEntry);
        Assert.Equal("Teklif Verildi", results[1].Name);
    }

    [Fact]
    public async Task Exposes_the_stage_kind_so_a_client_never_has_to_guess_won_and_lost_from_a_label()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        var definition = PipelineDefinition.Create(tenant, "Sales");
        seed.PipelineDefinitions.Add(definition);
        await seed.SaveChangesAsync();
        var version = definition.AddVersion(1);
        seed.PipelineDefinitionVersions.Add(version);
        await seed.SaveChangesAsync();
        seed.PipelineStages.AddRange(version.AddStage("Bekliyor", 0), version.AddWonStage("Kazanıldı", 10), version.AddLostStage("Kaybedildi", 11));
        await seed.SaveChangesAsync();

        await using var context = _fixture.CreateAdminContext();
        var results = await new GetPipelineStagesHandler(context, StubAuthorizer.AlwaysAllow)
            .HandleAsync(new GetPipelineStagesQuery(tenant, version.Id, TestData.Seller, Guid.NewGuid()));

        Assert.Equal([PipelineStageKind.Open, PipelineStageKind.Won, PipelineStageKind.Lost], results.Select(stage => stage.Kind));
    }

    /// <summary>Test-gap audit §7 (2026-09-19): GetPipelineStagesHandler previously had
    /// no authorization call at all — RLS alone kept results tenant-scoped, but any
    /// authenticated caller in the tenant could read pipeline configuration regardless
    /// of whether they held `crm.opportunity.read`.</summary>
    [Fact]
    public async Task Denies_when_the_caller_has_no_read_grant()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        var definition = PipelineDefinition.Create(tenant, "Sales");
        seed.PipelineDefinitions.Add(definition);
        await seed.SaveChangesAsync();
        var version = definition.AddVersion(1);
        seed.PipelineDefinitionVersions.Add(version);
        await seed.SaveChangesAsync();

        await using var context = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<OpportunityAuthorizationDeniedException>(() =>
            new GetPipelineStagesHandler(context, StubAuthorizer.AlwaysDeny)
                .HandleAsync(new GetPipelineStagesQuery(tenant, version.Id, TestData.Seller, Guid.NewGuid())));
    }
}
