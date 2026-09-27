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
        version.Publish();
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

    /// <summary>Legacy test amended 2026-09-27: previously tested silent null-stage opening
    /// when no pipeline existed. Now seeds a pipeline first (as Task 8 EnableModuleCommand
    /// guarantees all CRM tenants have one) and verifies successful opening with the
    /// resolved entry stage. The old silent-null behavior is now tested explicitly as a
    /// hard error in the new PipelineNotProvisionedException tests.</summary>
    [Fact]
    public async Task Opening_leaves_pipeline_fields_null_when_the_tenant_has_no_pipeline()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");

        // Seed a minimal published pipeline (as Task 8 guarantees)
        var definition = PipelineDefinition.Create(tenant, "Sales");
        seed.PipelineDefinitions.Add(definition);
        await seed.SaveChangesAsync();
        var version = definition.AddVersion(1);
        seed.PipelineDefinitionVersions.Add(version);
        await seed.SaveChangesAsync();
        var entryStage = version.AddStage("Entry Stage", 0);
        seed.PipelineStages.Add(entryStage);
        version.Publish();
        await seed.SaveChangesAsync();

        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        opportunity.AddLine(TestData.ProductRef(tenant), 1, 1000m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        var command = new OpenOpportunityCommand(
            tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion,
            DateTimeOffset.UtcNow.AddDays(7), "key-2", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        var result = await new OpenOpportunityHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        Assert.NotNull(result.PipelineStageId);
        Assert.Equal(entryStage.Id, result.PipelineStageId);
    }

    /// <summary>Test-gap audit §9 (2026-09-19): ResolveEntryStageAsync's documented
    /// choice ("this plan's own scope note") when a tenant has more than one
    /// PipelineDefinition — always the oldest (lowest Id). Regression-lock, not new
    /// behavior.</summary>
    [Fact]
    public async Task Opening_uses_the_oldest_pipeline_definition_when_a_tenant_has_more_than_one()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");

        var older = PipelineDefinition.Create(tenant, "Sales (older)");
        seed.PipelineDefinitions.Add(older);
        await seed.SaveChangesAsync();
        var olderVersion = older.AddVersion(1);
        seed.PipelineDefinitionVersions.Add(olderVersion);
        await seed.SaveChangesAsync();
        var olderEntryStage = olderVersion.AddStage("Bekliyor", 0);
        seed.PipelineStages.Add(olderEntryStage);
        olderVersion.Publish();
        await seed.SaveChangesAsync();

        var newer = PipelineDefinition.Create(tenant, "Sales (newer)");
        seed.PipelineDefinitions.Add(newer);
        await seed.SaveChangesAsync();
        var newerVersion = newer.AddVersion(1);
        seed.PipelineDefinitionVersions.Add(newerVersion);
        await seed.SaveChangesAsync();
        var newerEntryStage = newerVersion.AddStage("Yeni Bekliyor", 0);
        seed.PipelineStages.Add(newerEntryStage);
        newerVersion.Publish();
        await seed.SaveChangesAsync();

        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        opportunity.AddLine(TestData.ProductRef(tenant), 1, 1000m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        var command = new OpenOpportunityCommand(
            tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion,
            DateTimeOffset.UtcNow.AddDays(7), "key-multi-pipeline", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        var result = await new OpenOpportunityHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        Assert.Equal(olderEntryStage.Id, result.PipelineStageId);
    }

    /// <summary>Test-gap audit §9: version pinning is true by construction (no handler
    /// re-resolves an open Opportunity's pipeline fields after Open()), but was not
    /// explicitly regression-locked by a test that creates a second version afterward.</summary>
    [Fact]
    public async Task Opened_opportunity_keeps_its_original_version_after_a_newer_version_is_published()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");

        var definition = PipelineDefinition.Create(tenant, "Sales");
        seed.PipelineDefinitions.Add(definition);
        await seed.SaveChangesAsync();
        var v1 = definition.AddVersion(1);
        seed.PipelineDefinitionVersions.Add(v1);
        await seed.SaveChangesAsync();
        var v1EntryStage = v1.AddStage("Bekliyor", 0);
        seed.PipelineStages.Add(v1EntryStage);
        v1.Publish();
        await seed.SaveChangesAsync();

        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        opportunity.AddLine(TestData.ProductRef(tenant), 1, 1000m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        var command = new OpenOpportunityCommand(
            tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion,
            DateTimeOffset.UtcNow.AddDays(7), "key-version-pinning", Guid.NewGuid());
        await using (var openContext = _fixture.CreateAdminContext())
            await new OpenOpportunityHandler(openContext, StubAuthorizer.AlwaysAllow).HandleAsync(command);

        // A newer version is published for the same PipelineDefinition after opening.
        await using var seedV2 = _fixture.CreateAdminContext();
        var trackedDefinition = await seedV2.PipelineDefinitions.SingleAsync(d => d.Id == definition.Id);
        var v2 = trackedDefinition.AddVersion(2);
        seedV2.PipelineDefinitionVersions.Add(v2);
        await seedV2.SaveChangesAsync();
        var v2EntryStage = v2.AddStage("Yeni Bekliyor", 0);
        seedV2.PipelineStages.Add(v2EntryStage);
        v2.Publish();
        await seedV2.SaveChangesAsync();

        await using var verify = _fixture.CreateAdminContext();
        var reloaded = await verify.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunity.Id);
        Assert.Equal(v1.Id, reloaded.PipelineDefinitionVersionId);
        Assert.Equal(v1EntryStage.Id, reloaded.PipelineStageId);
    }

    /// <summary>Invariant table row 3 (2026-09-19 entry-stage resolution): a version
    /// exists but has zero stages flagged IsEntry — here, zero stages at all, the
    /// simplest way to construct that state. Must be a hard error, never a silent
    /// null-stage open (unlike "tenant has no pipeline configured at all", which stays
    /// legal per the test above).</summary>
    [Fact]
    public async Task Opening_throws_when_the_pipeline_version_has_no_entry_stage()
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
        version.Publish();
        await seed.SaveChangesAsync();
        // No AddStage call — the version has zero stages, so zero can be flagged IsEntry.

        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        opportunity.AddLine(TestData.ProductRef(tenant), 1, 1000m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        var command = new OpenOpportunityCommand(
            tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion,
            DateTimeOffset.UtcNow.AddDays(7), "key-3", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        var exception = await Assert.ThrowsAsync<PipelineConfigurationInvalidException>(() =>
            new OpenOpportunityHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command));
        Assert.Contains(version.Id.ToString(), exception.Message);

        var untouched = await context.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunity.Id);
        Assert.Equal(OpportunityStatus.Draft, untouched.Status);
    }

    /// <summary>Invariant table row 4: the version's entry stage exists but is
    /// IsActive == false — not a usable entry stage, must also be a hard error.</summary>
    [Fact]
    public async Task Opening_throws_when_the_pipeline_versions_entry_stage_is_inactive()
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
        entryStage.Deactivate();
        version.Publish();
        await seed.SaveChangesAsync();

        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        opportunity.AddLine(TestData.ProductRef(tenant), 1, 1000m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        var command = new OpenOpportunityCommand(
            tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion,
            DateTimeOffset.UtcNow.AddDays(7), "key-4", Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        var exception = await Assert.ThrowsAsync<PipelineConfigurationInvalidException>(() =>
            new OpenOpportunityHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command));
        Assert.Contains(version.Id.ToString(), exception.Message);

        var untouched = await context.Opportunities.AsNoTracking().SingleAsync(o => o.Id == opportunity.Id);
        Assert.Equal(OpportunityStatus.Draft, untouched.Status);
    }

    [Fact]
    public async Task HandleAsync_throws_when_the_tenant_has_no_pipeline_definition_at_all()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");
        // no PipelineDefinition seeded for this tenant at all
        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        opportunity.AddLine(TestData.ProductRef(tenant), 1, 1000m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        var command = new OpenOpportunityCommand(
            tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion,
            DateTimeOffset.UtcNow.AddDays(7), Guid.NewGuid().ToString(), Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<PipelineNotProvisionedException>(() =>
            new OpenOpportunityHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command));
    }

    [Fact]
    public async Task HandleAsync_throws_when_the_pipeline_definition_has_no_published_version()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await using var seedMasterData = _fixture.CreateMasterDataContext();
        var partyRef = await TestData.CreatePartyAsync(seedMasterData, tenant, "Acme");

        var definition = PipelineDefinition.Create(tenant, "Sales");
        seed.PipelineDefinitions.Add(definition);
        await seed.SaveChangesAsync();
        // definition.Id assigned, no version added — stays unpublished
        var opportunity = Opportunity.Create(tenant, partyRef, TestData.Seller, "TRY", 1000m);
        opportunity.AddLine(TestData.ProductRef(tenant), 1, 1000m);
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        var command = new OpenOpportunityCommand(
            tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion,
            DateTimeOffset.UtcNow.AddDays(7), Guid.NewGuid().ToString(), Guid.NewGuid());

        await using var context = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<PipelineNotProvisionedException>(() =>
            new OpenOpportunityHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command));
    }
}
