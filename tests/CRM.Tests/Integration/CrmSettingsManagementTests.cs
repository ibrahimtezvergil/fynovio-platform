using System.Text.Json;
using Contracts;
using CRM.Application;
using CRM.Domain;
using CRM.Persistence;
using CRM.Tests;
using CRM.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CRM.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class CrmSettingsManagementTests(PostgresFixture fixture)
{
    private static readonly PrincipalRef Administrator = new("https://identity.test", "crm-settings-admin");

    [Fact]
    public async Task Catalog_create_is_idempotent_and_commits_audit_and_outbox()
    {
        var tenant = TestData.NextTenant();
        var command = new ManageCrmCatalogCommand(tenant, Administrator, CrmCatalogKind.OpportunityType, null, 0,
            "renewal", "Renewal", null, 0, ConfigurationStatus.Active, "catalog-create-once", Guid.NewGuid());
        ManageCrmCatalogResult first;
        await using (var context = fixture.CreateAdminContext())
            first = await new ManageCrmCatalogHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command);
        await using (var context = fixture.CreateAdminContext())
        {
            var replay = await new ManageCrmCatalogHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command with { CorrelationId = Guid.NewGuid() });
            Assert.True(replay.Replayed);
            Assert.Equal(first.Item.Id, replay.Item.Id);
            Assert.Single(await context.OpportunityTypes.AsNoTracking().Where(x => x.TenantId == tenant).ToListAsync());
            Assert.Single(await context.OutboxMessages.AsNoTracking().Where(x => x.TenantId == tenant && x.AggregateType == "OpportunityType" && x.AggregateId == first.Item.Id).ToListAsync());
            Assert.Single(await context.EvidenceRecords.AsNoTracking().Where(x => x.TenantId == tenant && x.AggregateType == "OpportunityType" && x.AggregateId == first.Item.Id).ToListAsync());
        }
    }

    [Fact]
    public async Task Runtime_role_can_create_catalog_and_pipeline_draft_with_preallocated_identities()
    {
        var tenant = TestData.NextTenant();
        var connectionString = await fixture.RuntimeConnectionStringAsync();
        await using (var context = PostgresFixture.CreateContext(connectionString))
        {
            var catalog = await new ManageCrmCatalogHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(
                new ManageCrmCatalogCommand(tenant, Administrator, CrmCatalogKind.CustomerNeed, null, 0,
                    string.Empty, "Planning", "Service", 10m, ConfigurationStatus.Active, "runtime-catalog", Guid.NewGuid()));
            Assert.True(catalog.Item.Id > 0);
        }
        await using (var context = PostgresFixture.CreateContext(connectionString))
        {
            var draft = await new CreatePipelineDraftHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(
                Draft(tenant, Guid.NewGuid(), null, 0, 0, "runtime-draft", [new("Entry", 1, true, true)]));
            Assert.True(draft.VersionId > 0);
        }
        await using var verify = fixture.CreateAdminContext();
        Assert.Single(await verify.CustomerNeeds.Where(x => x.TenantId == tenant).ToListAsync());
        Assert.Equal(3, await verify.PipelineStages.Where(x => x.TenantId == tenant).CountAsync());
        Assert.Equal(2, await verify.OutboxMessages.CountAsync(x => x.TenantId == tenant));
        Assert.Equal(2, await verify.EvidenceRecords.CountAsync(x => x.TenantId == tenant));
    }

    [Fact]
    public async Task Pipeline_is_published_as_a_new_version_without_mutating_prior_version()
    {
        var tenant = TestData.NextTenant();
        var correlationId = Guid.NewGuid();
        CreatePipelineDraftResult first;
        await using (var context = fixture.CreateAdminContext())
        {
            first = await new CreatePipelineDraftHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(
                Draft(tenant, correlationId, null, 0, 0, "pipeline-v1", [new("Lead", 1, true, true)]));
        }
        await using (var context = fixture.CreateAdminContext())
        {
            var replay = await new CreatePipelineDraftHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(
                Draft(tenant, Guid.NewGuid(), null, 0, 0, "pipeline-v1", [new("Lead", 1, true, true)]));
            Assert.True(replay.Replayed);
            Assert.Equal(first.VersionId, replay.VersionId);
        }
        await using (var context = fixture.CreateAdminContext())
        {
            var publish = new PublishPipelineVersionCommand(tenant, Administrator, first.PipelineDefinitionId, first.VersionId, first.RowVersion,
                "pipeline-publish-v1", correlationId);
            var published = await new PublishPipelineVersionHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(publish);
            Assert.Equal(1, published.VersionNumber);
            Assert.Equal(0, published.OpportunitiesRetainedOnPriorVersions);
        }
        await using (var context = fixture.CreateAdminContext())
        {
            var replay = await new PublishPipelineVersionHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(
                new PublishPipelineVersionCommand(tenant, Administrator, first.PipelineDefinitionId, first.VersionId, first.RowVersion,
                    "pipeline-publish-v1", Guid.NewGuid()));
            Assert.True(replay.Replayed);
        }
        long originalStageId;
        await using (var context = fixture.CreateAdminContext())
        {
            var entry = await context.PipelineStages.SingleAsync(x => x.PipelineDefinitionVersionId == first.VersionId && x.IsEntry);
            originalStageId = entry.Id;
            var opportunity = Opportunity.Create(tenant, new PartyRef(tenant, 991), TestData.Seller, "TRY", 100m);
            opportunity.Open(DateTimeOffset.UtcNow.AddDays(5), first.VersionId, originalStageId);
            context.Opportunities.Add(opportunity);
            await context.SaveChangesAsync();
        }

        CreatePipelineDraftResult second;
        await using (var context = fixture.CreateAdminContext())
        {
            var definition = await context.PipelineDefinitions.AsNoTracking().SingleAsync(x => x.Id == first.PipelineDefinitionId);
            second = await new CreatePipelineDraftHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(
                Draft(tenant, correlationId, first.PipelineDefinitionId, definition.RowVersion, 1, "pipeline-v2",
                    [new("Qualification", 1, true, true), new("Proposal", 2, false, true)],
                    [new("Qualification", "Proposal")]));
        }
        await using (var context = fixture.CreateAdminContext())
        {
            var published = await new PublishPipelineVersionHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(
                new PublishPipelineVersionCommand(tenant, Administrator, first.PipelineDefinitionId, second.VersionId, second.RowVersion,
                    "pipeline-publish-v2", correlationId));
            Assert.Equal(2, published.VersionNumber);
            Assert.Equal(1, published.OpportunitiesRetainedOnPriorVersions);
            Assert.Equal(PipelineVersionStatus.Superseded, (await context.PipelineDefinitionVersions.SingleAsync(x => x.Id == first.VersionId)).Status);
            Assert.Equal(PipelineVersionStatus.Published, (await context.PipelineDefinitionVersions.SingleAsync(x => x.Id == second.VersionId)).Status);
            Assert.Equal(4, await context.PipelineStages.CountAsync(x => x.PipelineDefinitionVersionId == second.VersionId));
            var retained = await context.Opportunities.SingleAsync(x => x.PipelineDefinitionVersionId == first.VersionId);
            Assert.Equal(originalStageId, retained.PipelineStageId);
        }
        // Create a second active pipeline so the first can be deactivated
        await using (var context = fixture.CreateAdminContext())
        {
            var secondDefinition = PipelineDefinition.Create(tenant, "Secondary");
            context.PipelineDefinitions.Add(secondDefinition);
            await context.SaveChangesAsync();
        }

        SetPipelineLifecycleCommand lifecycle;
        await using (var context = fixture.CreateAdminContext())
        {
            var currentVersion = await context.PipelineDefinitions.Where(x => x.Id == first.PipelineDefinitionId)
                .Select(x => x.RowVersion).SingleAsync();
            lifecycle = new SetPipelineLifecycleCommand(tenant, Administrator, first.PipelineDefinitionId, currentVersion,
                false, false, false, "pipeline-deactivate", Guid.NewGuid());
            await new SetPipelineLifecycleHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(lifecycle);
        }
        await using (var context = fixture.CreateAdminContext())
        {
            var replay = await new SetPipelineLifecycleHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(
                lifecycle with { CorrelationId = Guid.NewGuid() });
            Assert.True(replay.Replayed);
        }
        SetPipelineLifecycleResult archived;
        await using (var context = fixture.CreateAdminContext())
        {
            var currentVersion = await context.PipelineDefinitions.Where(x => x.Id == first.PipelineDefinitionId)
                .Select(x => x.RowVersion).SingleAsync();
            archived = await new SetPipelineLifecycleHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(
                new SetPipelineLifecycleCommand(tenant, Administrator, first.PipelineDefinitionId, currentVersion,
                    false, true, false, "pipeline-archive", Guid.NewGuid()));
            Assert.True(archived.IsArchived);
            Assert.False(archived.IsActive);
        }
        await using (var context = fixture.CreateAdminContext())
        {
            var restored = await new SetPipelineLifecycleHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(
                new SetPipelineLifecycleCommand(tenant, Administrator, first.PipelineDefinitionId, archived.RowVersion,
                    false, false, true, "pipeline-restore", Guid.NewGuid()));
            Assert.False(restored.IsArchived);
            Assert.False(restored.IsActive);
            var retained = await context.Opportunities.SingleAsync(x => x.TenantId == tenant);
            Assert.Equal(first.VersionId, retained.PipelineDefinitionVersionId);
            Assert.Equal(originalStageId, retained.PipelineStageId);
            Assert.Equal(3, await context.OutboxMessages.CountAsync(x => x.TenantId == tenant && x.EventType == "enterprise.crm.pipeline.lifecycle_changed.v1"));
            Assert.Equal(3, await context.EvidenceRecords.CountAsync(x => x.TenantId == tenant && x.Action == "CrmPipeline.SetLifecycle"));
        }
    }

    [Fact]
    public async Task Settings_reject_a_stale_row_version()
    {
        var tenant = TestData.NextTenant();
        var command = new UpdateCrmSettingsCommand(tenant, Administrator, 0, null, OpportunityCreationMode.Form,
            null, true, true, AssignmentMode.Manual, AssignmentPolicy.AnyAssignablePrincipal, null, null, null, "settings-v1", Guid.NewGuid());
        await using (var context = fixture.CreateAdminContext())
            await new UpdateCrmSettingsHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command);
        await using (var context = fixture.CreateAdminContext())
        {
            var replay = await new UpdateCrmSettingsHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command with { CorrelationId = Guid.NewGuid() });
            Assert.True(replay.Replayed);
            Assert.Single(await context.EvidenceRecords.Where(x => x.TenantId == tenant && x.Action == CrmActionKeys.SettingsUpdate).ToListAsync());
            Assert.Single(await context.OutboxMessages.Where(x => x.TenantId == tenant && x.EventType == "enterprise.crm.settings.changed.v1").ToListAsync());
        }
        await using (var context = fixture.CreateAdminContext())
        {
            await Assert.ThrowsAsync<CrmSettingsConcurrencyConflictException>(() =>
                new UpdateCrmSettingsHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command with { IdempotencyKey = "settings-stale" }));
        }
    }

    [Theory]
    [InlineData(AssignmentMode.Team)]
    [InlineData(AssignmentMode.Territory)]
    public async Task Unavailable_assignment_provider_cannot_be_saved(AssignmentMode mode)
    {
        var tenant = TestData.NextTenant();
        var command = new UpdateCrmSettingsCommand(tenant, Administrator, 0, null, OpportunityCreationMode.Form,
            null, false, true, mode, AssignmentPolicy.AnyAssignablePrincipal, null, 41, 42,
            $"unsupported-{mode}", Guid.NewGuid());
        await using var context = fixture.CreateAdminContext();
        await Assert.ThrowsAsync<CrmAssignmentProviderUnavailableException>(() =>
            new UpdateCrmSettingsHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(command));
        Assert.False(await context.CrmSettings.AnyAsync(x => x.TenantId == tenant));
    }

    [Fact]
    public async Task Opportunity_creation_uses_the_tenant_default_type_and_default_assignee()
    {
        var tenant = TestData.NextTenant();
        var type = OpportunityType.Create(tenant, $"type-{Guid.NewGuid():N}", "Renewal");
        await using (var seedType = fixture.CreateAdminContext())
        {
            seedType.OpportunityTypes.Add(type);
            await seedType.SaveChangesAsync();
        }
        var defaultAssignee = new PrincipalRef("https://identity.test", "default-owner");
        var settings = CrmSettings.Create(tenant);
        settings.Replace(null, OpportunityCreationMode.Form, type.Id, false, true, AssignmentMode.DefaultPrincipal,
            AssignmentPolicy.AnyAssignablePrincipal, defaultAssignee, null, null);
        await using (var seed = fixture.CreateAdminContext())
        {
            seed.CrmSettings.Add(settings);
            await seed.SaveChangesAsync();
        }
        var command = new CreateOpportunityCommand(tenant, new PartyRef(tenant, 998), TestData.Seller,
            "TRY", 500m, "settings-defaults-create", Guid.NewGuid());
        await using var context = fixture.CreateAdminContext();
        var created = await new CreateOpportunityHandler(context, StubAuthorizer.AlwaysAllow, StubPartyIdentityResolver.Identity, StubDefinitionReader.None, StubLinkTargetDirectory.None).HandleAsync(command);
        var opportunity = await context.Opportunities.AsNoTracking().SingleAsync(x => x.Id == created.OpportunityId);
        Assert.Equal(type.Id, opportunity.OpportunityTypeId);
        Assert.Equal(defaultAssignee, opportunity.AssignedPrincipal);
    }

    [Fact]
    public async Task Manager_only_assignment_requires_the_crm_manager_override_action()
    {
        var tenant = TestData.NextTenant();
        var opportunity = Opportunity.Create(tenant, new PartyRef(tenant, 997), TestData.Seller, "TRY", 100m);
        var settings = CrmSettings.Create(tenant);
        settings.Replace(null, OpportunityCreationMode.Form, null, false, true, AssignmentMode.Manual,
            AssignmentPolicy.ManagerOnly, null, null, null);
        await using (var seed = fixture.CreateAdminContext())
        {
            seed.Opportunities.Add(opportunity);
            seed.CrmSettings.Add(settings);
            await seed.SaveChangesAsync();
        }
        var newOwner = new PrincipalRef("https://identity.test", "next-owner");
        var command = new ReassignOpportunityCommand(tenant, opportunity.Id, TestData.Seller, opportunity.RowVersion,
            newOwner, "manager-only-reassign", Guid.NewGuid());
        var authorizer = new RecordingAuthorizer();
        await using var context = fixture.CreateAdminContext();
        await new ReassignOpportunityHandler(context, authorizer, StubPrincipalDirectory.Permitting(newOwner)).HandleAsync(command);
        Assert.Contains(CrmActionKeys.AssignmentManage, authorizer.Actions);
    }

    [Fact]
    public async Task Unconfigured_settings_preserve_the_existing_first_active_pipeline_default()
    {
        var tenant = TestData.NextTenant();
        long firstId;
        await using (var seed = fixture.CreateAdminContext())
        {
            var first = PipelineDefinition.Create(tenant, "Original");
            var later = PipelineDefinition.Create(tenant, "Later");
            seed.PipelineDefinitions.AddRange(first, later);
            await seed.SaveChangesAsync();
            firstId = first.Id;
            var firstVersion = first.AddVersion(1);
            var laterVersion = later.AddVersion(1);
            seed.PipelineDefinitionVersions.AddRange(firstVersion, laterVersion);
            await seed.SaveChangesAsync();
            var firstStage = firstVersion.AddStage("Entry", 1);
            var laterStage = laterVersion.AddStage("Entry", 1);
            firstVersion.Publish();
            laterVersion.Publish();
            seed.PipelineStages.AddRange(firstStage, laterStage);
            await seed.SaveChangesAsync();
        }
        await using var context = fixture.CreateAdminContext();
        var actual = await new GetCrmSettingsHandler(context, StubAuthorizer.AlwaysAllow).HandleAsync(
            new GetCrmSettingsQuery(tenant, Administrator, Guid.NewGuid()));
        Assert.Equal(firstId, actual.DefaultPipelineDefinitionId);
        Assert.Equal("Form", actual.OpportunityCreationMode);
        Assert.Equal("Manual", actual.DefaultAssignmentMode);
        Assert.Equal("AnyAssignablePrincipal", actual.AssignmentPolicy);
        var responseJson = JsonSerializer.SerializeToElement(actual, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("Form", responseJson.GetProperty("opportunityCreationMode").GetString());
        Assert.Equal("Manual", responseJson.GetProperty("defaultAssignmentMode").GetString());
        Assert.Equal("AnyAssignablePrincipal", responseJson.GetProperty("assignmentPolicy").GetString());
    }

    [Fact]
    public async Task Runtime_role_is_tenant_isolated_for_settings_and_catalogs_and_rejects_cross_tenant_writes()
    {
        var tenantA = TestData.NextTenant();
        var tenantB = TestData.NextTenant();
        long typeAId;
        await using (var seed = fixture.CreateAdminContext())
        {
            var typeA = OpportunityType.Create(tenantA, $"type-a-{Guid.NewGuid():N}", "Tenant A type");
            seed.OpportunityTypes.Add(typeA);
            seed.CrmSettings.Add(CrmSettings.Create(tenantA));
            await seed.SaveChangesAsync();
            typeAId = typeA.Id;
        }
        await using (var runtime = PostgresFixture.CreateContext(await fixture.RuntimeConnectionStringAsync()))
        await using (var transaction = await runtime.Database.BeginTransactionAsync())
        {
            await runtime.SetTenantContextAsync(tenantB);
            Assert.Empty(await runtime.CrmSettings.ToListAsync());
            Assert.Empty(await runtime.OpportunityTypes.ToListAsync());
            runtime.OpportunityTypes.Add(OpportunityType.Create(tenantA, $"cross-{Guid.NewGuid():N}", "Cross tenant"));
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => runtime.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        }
        await using var verify = fixture.CreateAdminContext();
        Assert.Equal("Tenant A type", (await verify.OpportunityTypes.SingleAsync(x => x.Id == typeAId)).Name);
    }

    private CreatePipelineDraftCommand Draft(TenantId tenant, Guid correlationId, long? pipelineId, long expectedRowVersion,
        int expectedLatest, string key, IReadOnlyList<PipelineStageInput> stages,
        IReadOnlyList<PipelineTransitionInput>? transitions = null) => new(tenant, Administrator, pipelineId,
        "Sales", expectedRowVersion, expectedLatest, stages, transitions is { Count: > 0 }, transitions ?? [], key, correlationId);
}
