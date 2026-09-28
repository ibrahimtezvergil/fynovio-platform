using Contracts;
using CRM.Persistence;
using Host.Bootstrap;
using Host.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Host.Tests.Authentication;

/// <summary>Task 9 — one-time retroactive provisioning for tenants enabled before Task 8 shipped.
/// A tenant that was CRM-enabled but has no pipeline gets one from this command (idempotent).</summary>
[Collection(HostIntegrationCollection.Name)]
public sealed class BackfillCrmPipelinesCommandTests : IClassFixture<AuthApiFixture>
{
    private static readonly Dictionary<string, string?> Enabled = new() { ["Bootstrap__Enabled"] = "true" };

    private readonly AuthApiFixture _fixture;

    public BackfillCrmPipelinesCommandTests(AuthApiFixture fixture) => _fixture = fixture;

    private static async Task<(int Code, string Output, string Error)> RunBackfillAsync(AuthApiHost host, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await BackfillCrmPipelinesCommand.RunAsync(
            host.Services, host.Services.GetRequiredService<IConfiguration>(), [BackfillCrmPipelinesCommand.Name, .. args], output, error);
        return (code, output.ToString(), error.ToString());
    }

    private static async Task<(int Code, string Output)> BootstrapAsync(AuthApiHost host, long tenant, string email)
    {
        var output = new StringWriter();
        var code = await BootstrapCommand.RunAsync(
            host.Services, host.Services.GetRequiredService<IConfiguration>(),
            [BootstrapCommand.Name, "--tenant-id", tenant.ToString(), "--email", email, "--display-name", "Ops Admin"], output, new StringWriter());
        return (code, output.ToString());
    }

    private static async Task EnableCrmAsync(AuthApiHost host, long tenant)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var exitCode = await EnableModuleCommand.EnableAsync(scope.ServiceProvider, new TenantId(tenant), "crm", new StringWriter(), new StringWriter(), CancellationToken.None);
        Assert.Equal(EnableModuleCommand.Success, exitCode);
    }

    [Fact]
    public void The_command_is_recognised_only_by_its_exact_name()
    {
        Assert.True(BackfillCrmPipelinesCommand.IsRequested([BackfillCrmPipelinesCommand.Name]));
        Assert.False(BackfillCrmPipelinesCommand.IsRequested([]));
        Assert.False(BackfillCrmPipelinesCommand.IsRequested(["backfill"]));
    }

    [Fact]
    public async Task It_refuses_to_run_unless_bootstrap_is_explicitly_enabled()
    {
        using var host = await _fixture.StartHostAsync();

        var (code, output, error) = await RunBackfillAsync(host, "--tenant-ids", "1");

        Assert.Equal(BackfillCrmPipelinesCommand.NotPermitted, code);
        Assert.Empty(output);
        Assert.Contains("Bootstrap:Enabled", error);
    }

    [Fact]
    public async Task It_refuses_when_tenant_ids_are_missing()
    {
        using var host = await _fixture.StartHostAsync(Enabled);

        var (code, output, error) = await RunBackfillAsync(host);

        Assert.Equal(BackfillCrmPipelinesCommand.BadArguments, code);
        Assert.Contains("Usage:", error);
    }

    [Fact]
    public async Task RunAsync_provisions_a_pipeline_for_a_crm_enabled_tenant_stuck_pipeline_less()
    {
        var tenantId = AuthApiFixture.NewTenantId();
        var tenantIdObject = new TenantId(tenantId);
        using var host = await _fixture.StartHostAsync(Enabled);

        // Bootstrap and enable CRM (which auto-provisions a pipeline via Task 8)
        Assert.Equal(BootstrapCommand.Success, (await BootstrapAsync(host, tenantId, AuthApiFixture.NewEmail())).Code);
        await EnableCrmAsync(host, tenantId);

        // Simulate the pre-Task-8 gap: delete the auto-provisioned pipeline directly
        // (this must run inside its own transaction with tenant context set, same as any other
        // tenant-scoped write in this codebase — RLS applies here too)
        await using (var deleteScope = host.Services.CreateAsyncScope())
        {
            var crmContext = deleteScope.ServiceProvider.GetRequiredService<CrmDbContext>();
            await using var deleteTransaction = await crmContext.Database.BeginTransactionAsync();
            await crmContext.SetTenantContextAsync(tenantIdObject, CancellationToken.None);

            // Delete in correct order: stages first (Restrict FK), then versions (Restrict FK), then definition
            var definitions = await crmContext.PipelineDefinitions
                .Where(d => d.TenantId == tenantIdObject)
                .ToListAsync();

            foreach (var definition in definitions)
            {
                var versions = await crmContext.PipelineDefinitionVersions
                    .Where(v => v.PipelineDefinitionId == definition.Id)
                    .ToListAsync();

                foreach (var version in versions)
                {
                    var stages = await crmContext.PipelineStages
                        .Where(s => s.PipelineDefinitionVersionId == version.Id)
                        .ToListAsync();
                    crmContext.PipelineStages.RemoveRange(stages);
                }

                crmContext.PipelineDefinitionVersions.RemoveRange(versions);
                crmContext.PipelineDefinitions.Remove(definition);
            }

            await crmContext.SaveChangesAsync();
            await deleteTransaction.CommitAsync();
        }

        // Now run the backfill command with the explicit tenant ID
        var (exitCode, output, error) = await RunBackfillAsync(host, "--tenant-ids", tenantId.ToString());

        Assert.Equal(BackfillCrmPipelinesCommand.Success, exitCode);
        Assert.Empty(error);
        Assert.Contains("Provisioned a default pipeline for 1 tenant(s)", output);

        // Verify the pipeline was recreated
        await using (var verifyScope = host.Services.CreateAsyncScope())
        {
            var crmContext = verifyScope.ServiceProvider.GetRequiredService<CrmDbContext>();
            await using var verifyTransaction = await crmContext.Database.BeginTransactionAsync();
            await crmContext.SetTenantContextAsync(tenantIdObject, CancellationToken.None);
            Assert.True(await crmContext.PipelineDefinitions.AnyAsync(p => p.TenantId == tenantIdObject));
        }
    }

    [Fact]
    public async Task Running_with_a_tenant_that_already_has_a_pipeline_is_a_no_op()
    {
        var tenantId = AuthApiFixture.NewTenantId();
        using var host = await _fixture.StartHostAsync(Enabled);

        // Bootstrap and enable CRM (which auto-provisions a pipeline per Task 8)
        Assert.Equal(BootstrapCommand.Success, (await BootstrapAsync(host, tenantId, AuthApiFixture.NewEmail())).Code);
        await EnableCrmAsync(host, tenantId);

        // Run the backfill command with a tenant ID that already has a pipeline
        // This must prove "0 because it already has one," not "0 because the tenant was invisible"
        var (code, output, _) = await RunBackfillAsync(host, "--tenant-ids", tenantId.ToString());

        Assert.Equal(BackfillCrmPipelinesCommand.Success, code);
        Assert.Contains("Provisioned a default pipeline for 0 tenant(s)", output);

        // Second run should still succeed and still report 0
        var (code2, output2, _) = await RunBackfillAsync(host, "--tenant-ids", tenantId.ToString());

        Assert.Equal(BackfillCrmPipelinesCommand.Success, code2);
        Assert.Contains("Provisioned a default pipeline for 0 tenant(s)", output2);
    }

    [Fact]
    public async Task RunAsync_adds_won_and_lost_stages_to_a_pre_existing_published_version_and_reassigns_closed_opportunities()
    {
        var tenantId = AuthApiFixture.NewTenantId();
        var tenantIdObject = new TenantId(tenantId);
        using var host = await _fixture.StartHostAsync(Enabled);

        // Bootstrap and enable CRM to ensure tenant is CRM-enabled
        Assert.Equal(BootstrapCommand.Success, (await BootstrapAsync(host, tenantId, AuthApiFixture.NewEmail())).Code);
        await EnableCrmAsync(host, tenantId);

        long openStageId = 0;
        long versionId = 0;
        long wonOpportunityId = 0;

        // Seed a tenant with a Published PipelineDefinitionVersion built the OLD way
        // (directly via AddStage + Publish, WITHOUT AddWonStage/AddLostStage)
        // so it has no Won/Lost stage — simulating data from before this feature.
        await using (var seedScope = host.Services.CreateAsyncScope())
        {
            var crmContext = seedScope.ServiceProvider.GetRequiredService<CrmDbContext>();
            await using var seedTransaction = await crmContext.Database.BeginTransactionAsync();
            await crmContext.SetTenantContextAsync(tenantIdObject, CancellationToken.None);

            // Create pipeline definition the old way
            var definition = CRM.Domain.PipelineDefinition.Create(tenantIdObject, "Old Pipeline");
            crmContext.PipelineDefinitions.Add(definition);
            await crmContext.SaveChangesAsync();

            // Add version
            var version = definition.AddVersion(1);
            crmContext.PipelineDefinitionVersions.Add(version);
            await crmContext.SaveChangesAsync();

            // Now add the entry stage after version is saved
            var openStage = version.AddStage("Open", 10);
            version.Publish();

            // Add stage to context and save
            crmContext.PipelineStages.Add(openStage);
            await crmContext.SaveChangesAsync();

            openStageId = openStage.Id;
            versionId = version.Id;

            // Verify the version has no Won/Lost stages before backfill
            var stageCount = await crmContext.PipelineStages
                .Where(s => s.PipelineDefinitionVersionId == versionId)
                .CountAsync();
            Assert.Equal(1, stageCount); // Only the Open stage

            await seedTransaction.CommitAsync();
        }

        // Now seed a Won opportunity whose PipelineStageId points at the version's only Open stage
        await using (var seedScope = host.Services.CreateAsyncScope())
        {
            var crmContext = seedScope.ServiceProvider.GetRequiredService<CrmDbContext>();
            await using var seedTransaction = await crmContext.Database.BeginTransactionAsync();
            await crmContext.SetTenantContextAsync(tenantIdObject, CancellationToken.None);

            var opportunity = CRM.Domain.Opportunity.Create(
                tenantIdObject,
                new Contracts.PartyRef(tenantIdObject, 1),
                new Contracts.PrincipalRef("https://idp.local", "test-seller"),
                "TRY",
                estimatedAmount: 1000m);

            opportunity.AddLine(
                new Contracts.EntityRef(tenantIdObject, "MasterData", "Product", 1),
                quantity: 1,
                unitPrice: 1000m);

            opportunity.Open(
                DateTimeOffset.UtcNow.AddDays(7),
                pipelineDefinitionVersionId: versionId,
                pipelineStageId: openStageId);

            // Win the opportunity (it stays on the Open stage, as would happen before the feature)
            opportunity.Win(wonStageId: null, requireActiveRequiredLine: true);

            crmContext.Opportunities.Add(opportunity);
            await crmContext.SaveChangesAsync();

            wonOpportunityId = opportunity.Id;
            await seedTransaction.CommitAsync();
        }

        // Run the backfill command
        var (exitCode, output, _) = await RunBackfillAsync(host, "--tenant-ids", tenantId.ToString());

        Assert.Equal(BackfillCrmPipelinesCommand.Success, exitCode);
        Assert.Contains("Added 2 Won/Lost system stage(s)", output); // Exactly 2: one Won, one Lost

        // Verify the Won/Lost stages were added
        await using (var verifyScope = host.Services.CreateAsyncScope())
        {
            var crmContext = verifyScope.ServiceProvider.GetRequiredService<CrmDbContext>();
            await using var verifyTransaction = await crmContext.Database.BeginTransactionAsync();
            await crmContext.SetTenantContextAsync(tenantIdObject, CancellationToken.None);

            // Verify both Won and Lost stages now exist
            var wonStage = await crmContext.PipelineStages
                .SingleAsync(s => s.PipelineDefinitionVersionId == versionId && s.Kind == CRM.Domain.PipelineStageKind.Won);
            var lostStage = await crmContext.PipelineStages
                .SingleAsync(s => s.PipelineDefinitionVersionId == versionId && s.Kind == CRM.Domain.PipelineStageKind.Lost);

            // Verify the opportunity was reassigned to the Won stage
            var reloadedOpportunity = await crmContext.Opportunities
                .SingleAsync(o => o.Id == wonOpportunityId);

            Assert.Equal(wonStage.Id, reloadedOpportunity.PipelineStageId);
            Assert.Equal(openStageId, reloadedOpportunity.ClosedFromStageId);

            await verifyTransaction.CommitAsync();
        }
    }

    [Fact]
    public async Task RunAsync_assigns_a_stage_to_an_open_stageless_opportunity_preferring_its_own_version()
    {
        var tenantId = AuthApiFixture.NewTenantId();
        var tenantIdObject = new TenantId(tenantId);
        using var host = await _fixture.StartHostAsync(Enabled);

        // Bootstrap and enable CRM
        Assert.Equal(BootstrapCommand.Success, (await BootstrapAsync(host, tenantId, AuthApiFixture.NewEmail())).Code);
        await EnableCrmAsync(host, tenantId);

        long entryStageId = 0;
        long versionId = 0;
        long stagelessOpportunityId = 0;

        // Seed an Open opportunity with PipelineDefinitionVersionId set but PipelineStageId null
        await using (var seedScope = host.Services.CreateAsyncScope())
        {
            var crmContext = seedScope.ServiceProvider.GetRequiredService<CrmDbContext>();
            await using var seedTransaction = await crmContext.Database.BeginTransactionAsync();
            await crmContext.SetTenantContextAsync(tenantIdObject, CancellationToken.None);

            // Create pipeline definition
            var definition = CRM.Domain.PipelineDefinition.Create(tenantIdObject, "Test Pipeline");
            crmContext.PipelineDefinitions.Add(definition);
            await crmContext.SaveChangesAsync();

            // Add version
            var version = definition.AddVersion(1);
            crmContext.PipelineDefinitionVersions.Add(version);
            await crmContext.SaveChangesAsync();

            // Add entry stage (first stage is automatically entry and active)
            var entryStage = version.AddStage("Open", 10);
            version.Publish();

            crmContext.PipelineStages.Add(entryStage);
            await crmContext.SaveChangesAsync();

            entryStageId = entryStage.Id;
            versionId = version.Id;

            // Create an Open opportunity with PipelineDefinitionVersionId set but NO PipelineStageId
            var opportunity = CRM.Domain.Opportunity.Create(
                tenantIdObject,
                new Contracts.PartyRef(tenantIdObject, 1),
                new Contracts.PrincipalRef("https://idp.local", "test-seller"),
                "TRY",
                estimatedAmount: 1000m);

            opportunity.AddLine(
                new Contracts.EntityRef(tenantIdObject, "MasterData", "Product", 1),
                quantity: 1,
                unitPrice: 1000m);

            // Open with version ID but NO stage ID (simulating pre-fix data). This row
            // predates ck_opportunities_stage_required_once_open (Task 19), which a live
            // Postgres CHECK constraint enforces on every INSERT with no way to opt out per
            // statement — and re-validates every EXISTING row on ADD CONSTRAINT, so the
            // constraint can't come back until the backfill below has actually fixed this
            // row. The app's runtime role (crmContext) doesn't own the table and can't ALTER
            // it, so drop it on a separate superuser connection, matching the real order a
            // production upgrade runs in: the constraint (Task 19) never exists until after
            // the backfill (Task 18) has cleaned up rows that predate it.
            opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: versionId, pipelineStageId: null);

            await using (var adminContext = AuthTestFixture.CreateCrmContext(_fixture.AdminConnectionString))
                await adminContext.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE crm.opportunities DROP CONSTRAINT ck_opportunities_stage_required_once_open");
            crmContext.Opportunities.Add(opportunity);
            await crmContext.SaveChangesAsync();
            stagelessOpportunityId = opportunity.Id;
            await seedTransaction.CommitAsync();
        }

        try
        {
            // Run the backfill command
            var (exitCode, output, _) = await RunBackfillAsync(host, "--tenant-ids", tenantId.ToString());

            Assert.Equal(BackfillCrmPipelinesCommand.Success, exitCode);
            Assert.Contains("Assigned a stage to", output);
        }
        finally
        {
            // Re-add now that the backfill has fixed the row — succeeds because ADD
            // CONSTRAINT validates existing rows, and this row now satisfies it.
            await using var adminContext = AuthTestFixture.CreateCrmContext(_fixture.AdminConnectionString);
            await adminContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE crm.opportunities ADD CONSTRAINT ck_opportunities_stage_required_once_open CHECK (status <> 'open' OR pipeline_stage_id IS NOT NULL)");
        }

        // Verify the opportunity now has the entry stage assigned
        await using (var verifyScope = host.Services.CreateAsyncScope())
        {
            var crmContext = verifyScope.ServiceProvider.GetRequiredService<CrmDbContext>();
            await using var verifyTransaction = await crmContext.Database.BeginTransactionAsync();
            await crmContext.SetTenantContextAsync(tenantIdObject, CancellationToken.None);

            var reloaded = await crmContext.Opportunities.SingleAsync(o => o.Id == stagelessOpportunityId);
            Assert.Equal(versionId, reloaded.PipelineDefinitionVersionId);
            Assert.Equal(entryStageId, reloaded.PipelineStageId);

            await verifyTransaction.CommitAsync();
        }
    }

    [Fact]
    public async Task RunAsync_falls_back_to_the_tenants_default_pipeline_when_the_opportunitys_own_version_is_null()
    {
        var tenantId = AuthApiFixture.NewTenantId();
        var tenantIdObject = new TenantId(tenantId);
        using var host = await _fixture.StartHostAsync(Enabled);

        // Bootstrap and enable CRM
        Assert.Equal(BootstrapCommand.Success, (await BootstrapAsync(host, tenantId, AuthApiFixture.NewEmail())).Code);
        await EnableCrmAsync(host, tenantId);

        long tenantDefaultVersionId = 0;
        long tenantDefaultEntryStageId = 0;
        long stagelessOpportunityId = 0;

        // Seed a default pipeline for the tenant
        await using (var seedScope = host.Services.CreateAsyncScope())
        {
            var crmContext = seedScope.ServiceProvider.GetRequiredService<CrmDbContext>();
            await using var seedTransaction = await crmContext.Database.BeginTransactionAsync();
            await crmContext.SetTenantContextAsync(tenantIdObject, CancellationToken.None);

            // Create a default pipeline
            var defaultDefinition = CRM.Domain.PipelineDefinition.Create(tenantIdObject, "Default Pipeline");
            crmContext.PipelineDefinitions.Add(defaultDefinition);
            await crmContext.SaveChangesAsync();

            // Add a published version
            var defaultVersion = defaultDefinition.AddVersion(1);
            crmContext.PipelineDefinitionVersions.Add(defaultVersion);
            await crmContext.SaveChangesAsync();

            // Add entry stage (first stage is automatically entry and active)
            var defaultEntryStage = defaultVersion.AddStage("Open", 10);
            defaultVersion.Publish();

            crmContext.PipelineStages.Add(defaultEntryStage);
            await crmContext.SaveChangesAsync();

            tenantDefaultVersionId = defaultVersion.Id;
            tenantDefaultEntryStageId = defaultEntryStage.Id;

            // Set this as the tenant's default pipeline
            var crmSettings = CRM.Domain.CrmSettings.Create(tenantIdObject);
            crmSettings.Replace(
                defaultPipelineDefinitionId: defaultDefinition.Id,
                creationMode: CRM.Domain.OpportunityCreationMode.Wizard,
                defaultOpportunityTypeId: null,
                requireLostReason: false,
                requireWonLine: true,
                assignmentMode: CRM.Domain.AssignmentMode.Manual,
                assignmentPolicy: CRM.Domain.AssignmentPolicy.AnyAssignablePrincipal,
                defaultPrincipal: null,
                defaultTeamId: null,
                defaultTerritoryId: null);

            crmContext.CrmSettings.Add(crmSettings);
            await crmContext.SaveChangesAsync();

            await seedTransaction.CommitAsync();
        }

        // Seed a stageless opportunity with BOTH VersionId and StageId null
        await using (var seedScope = host.Services.CreateAsyncScope())
        {
            var crmContext = seedScope.ServiceProvider.GetRequiredService<CrmDbContext>();
            await using var seedTransaction = await crmContext.Database.BeginTransactionAsync();
            await crmContext.SetTenantContextAsync(tenantIdObject, CancellationToken.None);

            var opportunity = CRM.Domain.Opportunity.Create(
                tenantIdObject,
                new Contracts.PartyRef(tenantIdObject, 2),
                new Contracts.PrincipalRef("https://idp.local", "test-seller-2"),
                "EUR",
                estimatedAmount: 2000m);

            opportunity.AddLine(
                new Contracts.EntityRef(tenantIdObject, "MasterData", "Product", 1),
                quantity: 1,
                unitPrice: 2000m);

            // Open with BOTH version ID and stage ID null (pre-fix data without default
            // assignment). See the drop/re-add note above — same reason, same pattern: the
            // constraint can't come back until the backfill below has fixed this row.
            opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), pipelineDefinitionVersionId: null, pipelineStageId: null);

            await using (var adminContext = AuthTestFixture.CreateCrmContext(_fixture.AdminConnectionString))
                await adminContext.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE crm.opportunities DROP CONSTRAINT ck_opportunities_stage_required_once_open");
            crmContext.Opportunities.Add(opportunity);
            await crmContext.SaveChangesAsync();
            stagelessOpportunityId = opportunity.Id;
            await seedTransaction.CommitAsync();
        }

        try
        {
            // Run the backfill command
            var (exitCode, output, _) = await RunBackfillAsync(host, "--tenant-ids", tenantId.ToString());

            Assert.Equal(BackfillCrmPipelinesCommand.Success, exitCode);
            Assert.Contains("Assigned a stage to", output);
        }
        finally
        {
            await using var adminContext = AuthTestFixture.CreateCrmContext(_fixture.AdminConnectionString);
            await adminContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE crm.opportunities ADD CONSTRAINT ck_opportunities_stage_required_once_open CHECK (status <> 'open' OR pipeline_stage_id IS NOT NULL)");
        }

        // Verify the opportunity was assigned the tenant's default pipeline and stage
        await using (var verifyScope = host.Services.CreateAsyncScope())
        {
            var crmContext = verifyScope.ServiceProvider.GetRequiredService<CrmDbContext>();
            await using var verifyTransaction = await crmContext.Database.BeginTransactionAsync();
            await crmContext.SetTenantContextAsync(tenantIdObject, CancellationToken.None);

            var reloaded = await crmContext.Opportunities.SingleAsync(o => o.Id == stagelessOpportunityId);
            Assert.Equal(tenantDefaultVersionId, reloaded.PipelineDefinitionVersionId);
            Assert.Equal(tenantDefaultEntryStageId, reloaded.PipelineStageId);

            await verifyTransaction.CommitAsync();
        }
    }
}
