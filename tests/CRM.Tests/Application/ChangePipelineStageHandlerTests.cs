using Contracts;
using CRM.Application;
using CRM.Domain;
using CRM.Tests;
using CRM.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Application;

[Collection(nameof(PostgresCollection))]
public sealed class ChangePipelineStageHandlerTests
{
    private readonly PostgresFixture _fixture;

    public ChangePipelineStageHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Changing_to_an_active_stage_in_the_same_version_succeeds()
    {
        var (tenant, opportunityId, version, entryStageId, otherActiveStageId, _) = await SeedAsync();
        var opportunity = await LoadAsync(tenant, opportunityId);
        var command = new ChangePipelineStageCommand(
            tenant, opportunityId, TestData.Seller, opportunity.RowVersion, otherActiveStageId, "key-1", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        var result = await new ChangePipelineStageHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        Assert.False(result.Replayed);
        var reloaded = await context.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunityId);
        Assert.Equal(otherActiveStageId, reloaded.PipelineStageId);
    }

    [Fact]
    public async Task Changing_to_an_inactive_stage_is_rejected()
    {
        var (tenant, opportunityId, version, entryStageId, _, inactiveStageId) = await SeedAsync();
        var opportunity = await LoadAsync(tenant, opportunityId);
        var command = new ChangePipelineStageCommand(
            tenant, opportunityId, TestData.Seller, opportunity.RowVersion, inactiveStageId, "key-2", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<InvalidPipelineTransitionException>(() =>
            new ChangePipelineStageHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command));
    }

    /// <summary>Test-gap audit §9 (2026-09-19): the resolved "no transition matrix"
    /// model has no guard against `ChangeStage(currentStageId)` — presumably a legal
    /// no-op, but nothing proved it doesn't corrupt RowVersion/evidence/outbox. This
    /// regression-locks today's actual behavior: the write still happens exactly once
    /// (RowVersion advances by 1, not 0 or 2), the stage is unchanged, and the call
    /// succeeds rather than throwing.</summary>
    [Fact]
    public async Task Changing_to_the_current_stage_is_a_legal_single_write_no_op()
    {
        var (tenant, opportunityId, _, entryStageId, _, _) = await SeedAsync();
        var opportunity = await LoadAsync(tenant, opportunityId);
        var startingVersion = opportunity.RowVersion;
        var command = new ChangePipelineStageCommand(
            tenant, opportunityId, TestData.Seller, opportunity.RowVersion, entryStageId, "key-same-stage", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        var result = await new ChangePipelineStageHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        Assert.False(result.Replayed);
        Assert.Equal(entryStageId, result.PipelineStageId);
        var reloaded = await context.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunityId);
        Assert.Equal(entryStageId, reloaded.PipelineStageId);
        Assert.Equal(startingVersion + 1, reloaded.RowVersion);
    }

    [Fact]
    public async Task Changing_to_a_stage_from_a_different_version_is_rejected()
    {
        var (tenant, opportunityId, _, _, _, _) = await SeedAsync();
        var opportunity = await LoadAsync(tenant, opportunityId);

        await using var seed = _fixture.CreateAdminContext();
        var otherDefinition = PipelineDefinition.Create(tenant, "Support");
        seed.PipelineDefinitions.Add(otherDefinition);
        await seed.SaveChangesAsync();
        var otherVersion = otherDefinition.AddVersion(1);
        seed.PipelineDefinitionVersions.Add(otherVersion);
        await seed.SaveChangesAsync();
        var foreignStage = otherVersion.AddStage("Farklı Süreç", 0);
        seed.PipelineStages.Add(foreignStage);
        await seed.SaveChangesAsync();

        var command = new ChangePipelineStageCommand(
            tenant, opportunityId, TestData.Seller, opportunity.RowVersion, foreignStage.Id, "key-3", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<InvalidPipelineTransitionException>(() =>
            new ChangePipelineStageHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command));
    }

    private async Task<(TenantId TenantId, long OpportunityId, PipelineDefinitionVersion Version, long EntryStageId, long OtherActiveStageId, long InactiveStageId)> SeedAsync()
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
        var otherActiveStage = version.AddStage("Teklif Verildi", 1);
        var inactiveStage = version.AddStage("Eski Aşama", 2);
        inactiveStage.Deactivate();
        seed.PipelineStages.AddRange(entryStage, otherActiveStage, inactiveStage);
        await seed.SaveChangesAsync();

        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        opportunity.AddLine(TestData.ProductRef(tenant), 1, 1000m);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: version.Id, pipelineStageId: entryStage.Id);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        return (tenant, opportunity.Id, version, entryStage.Id, otherActiveStage.Id, inactiveStage.Id);
    }

    private async Task<Opportunity> LoadAsync(TenantId tenant, long opportunityId)
    {
        await using var context = _fixture.CreateAdminContext();
        return await context.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunityId);
    }
}
