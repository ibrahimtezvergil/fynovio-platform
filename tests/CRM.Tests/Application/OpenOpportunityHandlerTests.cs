using Contracts;
using CRM.Application;
using CRM.Domain;
using CRM.Tests;
using CRM.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Application;

[Collection(nameof(PostgresCollection))]
public sealed class OpenOpportunityHandlerTests
{
    private readonly PostgresFixture _fixture;

    public OpenOpportunityHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Opening_assigns_the_tenants_entry_stage_when_a_pipeline_exists()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");

        var definition = PipelineDefinition.Create(tenant, "Sales");
        seed.PipelineDefinitions.Add(definition);
        await seed.SaveChangesAsync();
        var version = definition.AddVersion(1);
        seed.PipelineDefinitionVersions.Add(version);
        await seed.SaveChangesAsync();
        var entryStage = version.AddStage("Bekliyor", 0);
        seed.PipelineStages.Add(entryStage);
        await seed.SaveChangesAsync();

        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        opportunity.AddLine(TestData.ProductRef(tenant), 1, 1000m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        var command = new OpenOpportunityCommand(
            tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion,
            DateTimeOffset.UtcNow.AddDays(7), "key-1", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        var result = await new OpenOpportunityHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        Assert.False(result.Replayed);
        Assert.Equal(entryStage.Id, result.PipelineStageId);

        var reloaded = await context.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunity.Id);
        Assert.Equal(version.Id, reloaded.PipelineDefinitionVersionId);
        Assert.Equal(entryStage.Id, reloaded.PipelineStageId);
    }

    [Fact]
    public async Task Opening_leaves_pipeline_fields_null_when_the_tenant_has_no_pipeline()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");
        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        opportunity.AddLine(TestData.ProductRef(tenant), 1, 1000m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        var command = new OpenOpportunityCommand(
            tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion,
            DateTimeOffset.UtcNow.AddDays(7), "key-2", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        var result = await new OpenOpportunityHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        Assert.Null(result.PipelineStageId);
    }
}
