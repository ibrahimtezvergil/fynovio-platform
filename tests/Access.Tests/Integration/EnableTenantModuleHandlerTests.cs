using Access.Application;
using Access.Domain.Authorization;
using Access.Domain.Identity;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Access.Tests.Integration;

/// <summary>Module enablement — the general, versioned "system template → tenant-local instance" copy that
/// replaces per-module dev seeding. Uses a made-up `demo` module so nothing here depends on CRM.</summary>
public sealed class EnableTenantModuleHandlerTests : IClassFixture<PostgresFixture>
{
    private const string Read = "demo.record.read";
    private const string Edit = "demo.record.edit";

    private static readonly ModuleCapabilityManifest DemoV1 = new(
        "demo", "Demo module", 1,
        [
            new PermissionSetTemplate("demo_read", "Demo read", [new(Read)]),
            new PermissionSetTemplate("demo_edit", "Demo edit", [new(Edit)])
        ],
        [
            new RoleTemplate("demo_viewer", "Demo viewer", ["demo_read"]),
            new RoleTemplate("demo_manager", "Demo manager", ["demo_read", "demo_edit"], GrantToTenantAdministrators: true)
        ]);

    private static readonly PrincipalRef Operator = new("fynovio-platform", "operator:test");

    private readonly PostgresFixture _fixture;

    public EnableTenantModuleHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    private static IEnumerable<ActionRegistryDescriptor> Registry() =>
        AccessActionCatalog.All.Concat(
        [
            new ActionRegistryDescriptor(Read, "Demo", "Record"),
            new ActionRegistryDescriptor(Edit, "Demo", "Record")
        ]);

    private static EnableTenantModuleHandler HandlerFor(AccessDbContext context, params ModuleCapabilityManifest[] manifests) =>
        new(context, new ModuleCapabilityCatalog(manifests), TimeProvider.System);

    private static async Task<(long AccountId, PrincipalRef Principal)> SeedMemberAsync(AccessDbContext context, TenantId tenantId, string label)
    {
        var principal = new PrincipalRef("test-idp", $"{label}-{Guid.NewGuid():N}");
        var account = Account.Create($"{principal.Subject}@test.local", principal.Subject);
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        context.ExternalIdentities.Add(ExternalIdentity.Link(account.Id, principal));
        var membership = TenantMembership.Invite(tenantId, account.Id);
        membership.Activate();
        context.TenantMemberships.Add(membership);
        await context.SaveChangesAsync();
        return (account.Id, principal);
    }

    /// <summary>A bootstrapped tenant with one administrator and one plain member.</summary>
    private async Task<(TenantId Tenant, (long AccountId, PrincipalRef Principal) Admin, (long AccountId, PrincipalRef Principal) Member)> BootstrappedTenantAsync()
    {
        var tenantId = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();
        await AccessActionCatalogSeeder.EnsureSeededAsync(seed, Registry());
        var admin = await SeedMemberAsync(seed, tenantId, "admin");
        var member = await SeedMemberAsync(seed, tenantId, "member");
        await new BootstrapTenantAccessHandler(seed).HandleAsync(new BootstrapTenantAccessCommand(tenantId, admin.Principal, Guid.NewGuid()));
        return (tenantId, admin, member);
    }

    private static EnableTenantModuleCommand Command(TenantId tenantId, string moduleKey = "demo") =>
        new(tenantId, moduleKey, Operator, Guid.NewGuid());

    [Fact]
    public async Task Enable_copies_the_template_into_tenant_local_rows_with_provenance()
    {
        var (tenant, _, _) = await BootstrappedTenantAsync();

        await using (var context = _fixture.CreateAdminContext())
        {
            var result = await HandlerFor(context, DemoV1).HandleAsync(Command(tenant));
            Assert.Equal(EnableTenantModuleStatus.Enabled, result.Status);
            Assert.Equal(1, result.EnabledVersion);
        }

        await using var verify = _fixture.CreateAdminContext();
        var roles = await verify.Roles.Where(r => r.TenantId == tenant && r.OriginModuleKey == "demo").ToListAsync();
        Assert.Equal(["demo_manager", "demo_viewer"], roles.Select(r => r.Key).Order());
        Assert.All(roles, r =>
        {
            Assert.Equal(Role.OriginSystemTemplate, r.Origin);
            Assert.Equal(1, r.OriginVersion);
        });

        var sets = await verify.PermissionSets.Include(p => p.Items).Where(p => p.TenantId == tenant && p.OriginModuleKey == "demo").ToListAsync();
        Assert.Equal(["demo_edit", "demo_read"], sets.Select(s => s.Key).Order());
        Assert.Equal([Edit], sets.Single(s => s.Key == "demo_edit").Items.Select(i => i.ActionKey));
        Assert.All(sets, s => Assert.Equal(1, s.OriginVersion));

        var manager = roles.Single(r => r.Key == "demo_manager");
        var managerSets = await verify.RolePermissionSets.Where(x => x.TenantId == tenant && x.RoleId == manager.Id)
            .Join(verify.PermissionSets, x => x.PermissionSetId, p => p.Id, (_, p) => p.Key).ToListAsync();
        Assert.Equal(["demo_edit", "demo_read"], managerSets.Order());

        var enablement = await verify.TenantModuleEnablements.SingleAsync(e => e.TenantId == tenant);
        Assert.Equal(("demo", 1), (enablement.ModuleKey, enablement.TemplateVersion));
    }

    [Fact]
    public async Task Enable_assigns_GrantToTenantAdministrators_roles_to_current_administrators_only()
    {
        var (tenant, admin, member) = await BootstrappedTenantAsync();

        await using (var context = _fixture.CreateAdminContext())
        {
            var result = await HandlerFor(context, DemoV1).HandleAsync(Command(tenant));
            Assert.Equal(1, result.GrantedAssignments);
        }

        await using var verify = _fixture.CreateAdminContext();
        var moduleAssignments = await verify.RoleAssignments
            .Where(a => a.TenantId == tenant && a.Source == RoleAssignment.SourceModuleEnablement).ToListAsync();

        var assignment = Assert.Single(moduleAssignments);
        Assert.Equal(admin.AccountId, assignment.AccountId);
        Assert.Equal(admin.AccountId, assignment.GrantedByAccountId);
        Assert.Contains("demo v1", assignment.Reason);
        var roleKey = await verify.Roles.Where(r => r.Id == assignment.RoleId).Select(r => r.Key).SingleAsync();
        Assert.Equal("demo_manager", roleKey);
        Assert.DoesNotContain(moduleAssignments, a => a.AccountId == member.AccountId);
    }

    [Fact]
    public async Task Module_actions_reach_the_administrator_set_only_on_enablement_and_keep_their_relation()
    {
        var (tenant, _, _) = await BootstrappedTenantAsync();
        var ownerScoped = DemoV1 with { PermissionSets = [new PermissionSetTemplate("demo_owner", "Demo owner", [new(Read), new(Edit, "owner")])], Roles = [] };

        await using (var before = _fixture.CreateAdminContext())
            Assert.Empty(await AdministratorGrantsAsync(before, tenant));

        await using (var context = _fixture.CreateAdminContext())
            Assert.Equal(EnableTenantModuleStatus.Enabled, (await HandlerFor(context, ownerScoped).HandleAsync(Command(tenant))).Status);

        await using var verify = _fixture.CreateAdminContext();
        Assert.Equal([(Edit, "owner"), (Read, (string?)null)], await AdministratorGrantsAsync(verify, tenant));
    }

    private static async Task<List<(string ActionKey, string? Relation)>> AdministratorGrantsAsync(AccessDbContext context, TenantId tenant) =>
        (await context.PermissionSets.Where(set => set.TenantId == tenant && set.Key == "tenant_administration")
            .SelectMany(set => set.Items).Where(item => item.ActionKey == Read || item.ActionKey == Edit)
            .Select(item => new { item.ActionKey, item.Relation }).ToListAsync())
        .Select(item => (item.ActionKey, item.Relation)).OrderBy(item => item.ActionKey).ToList();

    [Fact]
    public async Task Enable_bumps_the_access_revision_and_writes_one_evidence_and_one_outbox_row()
    {
        var (tenant, _, _) = await BootstrappedTenantAsync();
        long before;
        await using (var read = _fixture.CreateAdminContext())
            before = await read.TenantAccessStates.Where(s => s.TenantId == tenant).Select(s => s.Revision).SingleAsync();

        await using (var context = _fixture.CreateAdminContext())
            await HandlerFor(context, DemoV1).HandleAsync(Command(tenant));

        await using var verify = _fixture.CreateAdminContext();
        Assert.Equal(before + 1, await verify.TenantAccessStates.Where(s => s.TenantId == tenant).Select(s => s.Revision).SingleAsync());

        var evidence = await verify.EvidenceRecords.SingleAsync(e => e.TenantId == tenant && e.Action == "TenantModule.Enable");
        Assert.Equal(Operator.Subject, evidence.PrincipalSubject);
        using (var detail = System.Text.Json.JsonDocument.Parse(evidence.Detail))
        {
            Assert.Equal("demo", detail.RootElement.GetProperty("moduleKey").GetString());
            Assert.Equal(1, detail.RootElement.GetProperty("version").GetInt32());
        }

        var outbox = await verify.OutboxMessages.SingleAsync(m => m.TenantId == tenant && m.EventType == "enterprise.access.tenant_module.enabled.v1");
        Assert.Equal(evidence.AggregateId, outbox.AggregateId);
    }

    [Fact]
    public async Task A_second_enable_changes_nothing()
    {
        var (tenant, _, _) = await BootstrappedTenantAsync();
        await using (var first = _fixture.CreateAdminContext())
            await HandlerFor(first, DemoV1).HandleAsync(Command(tenant));
        var snapshot = await SnapshotAsync(tenant);

        EnableTenantModuleResult second;
        await using (var context = _fixture.CreateAdminContext())
            second = await HandlerFor(context, DemoV1).HandleAsync(Command(tenant));

        Assert.Equal(EnableTenantModuleStatus.AlreadyEnabled, second.Status);
        Assert.Equal(1, second.EnabledVersion);
        Assert.Equal(snapshot, await SnapshotAsync(tenant));
    }

    /// <summary>Phase 1.5 Decision A: no reconciler. A tenant enabled at v1 is never touched by a v2 manifest.</summary>
    [Fact]
    public async Task A_newer_template_version_never_changes_an_already_enabled_tenant()
    {
        var (tenant, _, _) = await BootstrappedTenantAsync();
        await using (var first = _fixture.CreateAdminContext())
            await HandlerFor(first, DemoV1).HandleAsync(Command(tenant));
        var snapshot = await SnapshotAsync(tenant);

        var demoV2 = DemoV1 with
        {
            Version = 2,
            PermissionSets = [.. DemoV1.PermissionSets, new PermissionSetTemplate("demo_extra", "Demo extra", [new(Edit)])],
            Roles = [.. DemoV1.Roles, new RoleTemplate("demo_extra_role", "Demo extra role", ["demo_extra"])]
        };

        EnableTenantModuleResult result;
        await using (var context = _fixture.CreateAdminContext())
            result = await HandlerFor(context, demoV2).HandleAsync(Command(tenant));

        Assert.Equal(EnableTenantModuleStatus.AlreadyEnabled, result.Status);
        Assert.Equal((1, 2), (result.EnabledVersion, result.LatestVersion));
        Assert.Equal(snapshot, await SnapshotAsync(tenant));

        await using var verify = _fixture.CreateAdminContext();
        Assert.False(await verify.Roles.AnyAsync(r => r.TenantId == tenant && r.Key == "demo_extra_role"));
        Assert.All(await verify.Roles.Where(r => r.TenantId == tenant && r.OriginModuleKey == "demo").ToListAsync(), r => Assert.Equal(1, r.OriginVersion));
    }

    [Fact]
    public async Task An_administrator_who_appears_after_enablement_gets_no_module_role()
    {
        var (tenant, _, _) = await BootstrappedTenantAsync();
        await using (var context = _fixture.CreateAdminContext())
            await HandlerFor(context, DemoV1).HandleAsync(Command(tenant));

        await using (var seed = _fixture.CreateAdminContext())
        {
            var late = await SeedMemberAsync(seed, tenant, "late-admin");
            var adminRole = await seed.Roles.SingleAsync(r => r.TenantId == tenant && r.Key == "tenant_administrator");
            seed.RoleAssignments.Add(RoleAssignment.Grant(tenant, late.AccountId, adminRole.Id, late.AccountId, RoleAssignment.SourceManual));
            await seed.SaveChangesAsync();

            var again = await HandlerFor(seed, DemoV1).HandleAsync(Command(tenant));
            Assert.Equal(EnableTenantModuleStatus.AlreadyEnabled, again.Status);

            var managerRole = await seed.Roles.SingleAsync(r => r.TenantId == tenant && r.Key == "demo_manager");
            Assert.False(await seed.RoleAssignments.AnyAsync(a => a.RoleId == managerRole.Id && a.AccountId == late.AccountId));
        }
    }

    [Fact]
    public async Task An_unknown_module_is_refused_and_writes_nothing()
    {
        var (tenant, _, _) = await BootstrappedTenantAsync();
        var snapshot = await SnapshotAsync(tenant);

        await using (var context = _fixture.CreateAdminContext())
            Assert.Equal(EnableTenantModuleStatus.UnknownModule, (await HandlerFor(context, DemoV1).HandleAsync(Command(tenant, "nope"))).Status);

        Assert.Equal(snapshot, await SnapshotAsync(tenant));
    }

    [Fact]
    public async Task A_tenant_that_was_never_bootstrapped_is_refused()
    {
        var tenant = TestData.NextTenant();
        await using var context = _fixture.CreateAdminContext();
        await AccessActionCatalogSeeder.EnsureSeededAsync(context, Registry());

        var result = await HandlerFor(context, DemoV1).HandleAsync(Command(tenant));

        Assert.Equal(EnableTenantModuleStatus.TenantNotBootstrapped, result.Status);
        Assert.False(await context.Roles.AnyAsync(r => r.TenantId == tenant));
    }

    [Fact]
    public async Task A_tenant_authored_role_with_a_template_key_blocks_enablement_and_is_left_untouched()
    {
        var (tenant, _, _) = await BootstrappedTenantAsync();
        await using (var seed = _fixture.CreateAdminContext())
        {
            seed.Roles.Add(Role.Create(tenant, "demo_viewer", "My own viewer"));
            await seed.SaveChangesAsync();
        }
        var snapshot = await SnapshotAsync(tenant);

        EnableTenantModuleResult result;
        await using (var context = _fixture.CreateAdminContext())
            result = await HandlerFor(context, DemoV1).HandleAsync(Command(tenant));

        Assert.Equal(EnableTenantModuleStatus.TemplateKeyConflict, result.Status);
        Assert.Contains("demo_viewer", result.Detail);
        Assert.Equal(snapshot, await SnapshotAsync(tenant));

        await using var verify = _fixture.CreateAdminContext();
        var own = await verify.Roles.SingleAsync(r => r.TenantId == tenant && r.Key == "demo_viewer");
        Assert.Equal((Role.OriginTenant, "My own viewer", (string?)null), (own.Origin, own.Name, own.OriginModuleKey));
        Assert.False(await verify.TenantModuleEnablements.AnyAsync(e => e.TenantId == tenant));
    }

    [Fact]
    public async Task A_manifest_naming_an_unregistered_action_fails_closed_and_persists_nothing()
    {
        var (tenant, _, _) = await BootstrappedTenantAsync();
        var snapshot = await SnapshotAsync(tenant);
        var broken = DemoV1 with
        {
            PermissionSets = [new PermissionSetTemplate("demo_read", "Demo read", [new(Read), new("demo.record.unregistered")])],
            Roles = [new RoleTemplate("demo_viewer", "Demo viewer", ["demo_read"])]
        };

        await using (var context = _fixture.CreateAdminContext())
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => HandlerFor(context, broken).HandleAsync(Command(tenant)));
            Assert.Contains("demo.record.unregistered", exception.Message);
        }

        Assert.Equal(snapshot, await SnapshotAsync(tenant));
    }

    [Fact]
    public async Task Concurrent_enables_of_one_tenant_enable_it_exactly_once()
    {
        var (tenant, _, _) = await BootstrappedTenantAsync();

        async Task<EnableTenantModuleResult> RunAsync()
        {
            await using var context = _fixture.CreateAdminContext();
            return await HandlerFor(context, DemoV1).HandleAsync(Command(tenant));
        }

        var results = await Task.WhenAll(RunAsync(), RunAsync(), RunAsync());

        Assert.Equal(1, results.Count(r => r.Status == EnableTenantModuleStatus.Enabled));
        Assert.Equal(2, results.Count(r => r.Status == EnableTenantModuleStatus.AlreadyEnabled));

        await using var verify = _fixture.CreateAdminContext();
        Assert.Equal(2, await verify.Roles.CountAsync(r => r.TenantId == tenant && r.OriginModuleKey == "demo"));
        Assert.Equal(1, await verify.TenantModuleEnablements.CountAsync(e => e.TenantId == tenant));
    }

    /// <summary>The copies are ordinary Access rows: the existing PDP, unchanged, reads them.</summary>
    [Fact]
    public async Task The_real_pdp_honours_the_enabled_roles()
    {
        var (tenant, admin, member) = await BootstrappedTenantAsync();
        await using (var context = _fixture.CreateAdminContext())
            await HandlerFor(context, DemoV1).HandleAsync(Command(tenant));

        await using (var seed = _fixture.CreateAdminContext())
        {
            var viewerRole = await seed.Roles.SingleAsync(r => r.TenantId == tenant && r.Key == "demo_viewer");
            seed.RoleAssignments.Add(RoleAssignment.Grant(tenant, member.AccountId, viewerRole.Id, admin.AccountId, RoleAssignment.SourceManual));
            await seed.SaveChangesAsync();
        }

        await using var context2 = _fixture.CreateAdminContext();
        var authorizer = new AccessAuthorizer(context2, new PrincipalResolver(context2), new AccessActionCatalogService(context2));
        var resource = new ResourceDescriptor("Record", 1, null);

        async Task<bool> AllowedAsync(PrincipalRef who, string action) =>
            (await authorizer.AuthorizeAsync(new AuthorizationRequest(new ActorContext(tenant, who, Guid.NewGuid()), new ActionKey(action), resource))).IsAllowed;

        Assert.True(await AllowedAsync(admin.Principal, Edit));
        Assert.True(await AllowedAsync(admin.Principal, Read));
        Assert.True(await AllowedAsync(member.Principal, Read));
        Assert.False(await AllowedAsync(member.Principal, Edit));
    }

    [Fact]
    public async Task Runtime_role_sees_only_its_own_tenants_enablements_and_cannot_write_for_another()
    {
        var (tenantA, _, _) = await BootstrappedTenantAsync();
        var (tenantB, _, _) = await BootstrappedTenantAsync();
        foreach (var tenant in new[] { tenantA, tenantB })
        {
            await using var context = _fixture.CreateAdminContext();
            await HandlerFor(context, DemoV1).HandleAsync(Command(tenant));
        }

        var runtime = await _fixture.RuntimeConnectionStringAsync();
        await using (var context = PostgresFixture.CreateContext(runtime))
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            await context.SetTenantContextAsync(tenantA, CancellationToken.None);

            var visible = await context.TenantModuleEnablements.Select(e => e.TenantId).ToListAsync();
            Assert.Equal([tenantA], visible);

            context.TenantModuleEnablements.Add(TenantModuleEnablement.Enable(tenantB, "another", 1, DateTimeOffset.UtcNow));
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
        }

        await using var noContext = PostgresFixture.CreateContext(runtime);
        await using var noTenant = await noContext.Database.BeginTransactionAsync();
        Assert.Empty(await noContext.TenantModuleEnablements.ToListAsync());
    }

    [Fact]
    public async Task The_database_rejects_one_sided_or_tenant_authored_provenance_and_unknown_assignment_sources()
    {
        await using var context = _fixture.CreateAdminContext();
        var tenant = TestData.NextTenant().Value;

        async Task<string> SqlStateOfAsync(FormattableString sql)
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(sql));
            return exception.SqlState;
        }

        // origin_module_key without origin_version
        Assert.Equal(PostgresErrorCodes.CheckViolation, await SqlStateOfAsync(
            $"INSERT INTO access.roles (tenant_id, key, name, origin, origin_module_key) VALUES ({tenant}, 'a', 'A', 'system_template', 'demo')"));
        // provenance on a tenant-authored row
        Assert.Equal(PostgresErrorCodes.CheckViolation, await SqlStateOfAsync(
            $"INSERT INTO access.permission_sets (tenant_id, key, name, origin, origin_module_key, origin_version) VALUES ({tenant}, 'a', 'A', 'tenant', 'demo', 1)"));
        // version 0
        Assert.Equal(PostgresErrorCodes.CheckViolation, await SqlStateOfAsync(
            $"INSERT INTO access.roles (tenant_id, key, name, origin, origin_module_key, origin_version) VALUES ({tenant}, 'b', 'B', 'system_template', 'demo', 0)"));
        // one enablement per (tenant, module)
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO access.tenant_module_enablements (tenant_id, module_key, template_version, enabled_at) VALUES ({tenant}, 'demo', 1, now())");
        Assert.Equal(PostgresErrorCodes.UniqueViolation, await SqlStateOfAsync(
            $"INSERT INTO access.tenant_module_enablements (tenant_id, module_key, template_version, enabled_at) VALUES ({tenant}, 'demo', 2, now())"));
    }

    private async Task<TenantSnapshot> SnapshotAsync(TenantId tenant)
    {
        await using var context = _fixture.CreateAdminContext();
        return new TenantSnapshot(
            await context.Roles.CountAsync(r => r.TenantId == tenant),
            await context.PermissionSets.CountAsync(p => p.TenantId == tenant),
            await context.PermissionSetItems.CountAsync(i => i.TenantId == tenant),
            await context.RolePermissionSets.CountAsync(x => x.TenantId == tenant),
            await context.RoleAssignments.CountAsync(a => a.TenantId == tenant),
            await context.TenantModuleEnablements.CountAsync(e => e.TenantId == tenant),
            await context.EvidenceRecords.CountAsync(e => e.TenantId == tenant),
            await context.OutboxMessages.CountAsync(m => m.TenantId == tenant),
            await context.TenantAccessStates.Where(s => s.TenantId == tenant).Select(s => s.Revision).SingleAsync());
    }

    private sealed record TenantSnapshot(
        int Roles, int PermissionSets, int Items, int RolePermissionSets, int Assignments,
        int Enablements, int Evidence, int Outbox, long Revision);
}
