using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Access.Domain.Authorization;
using Access.Domain.Identity;
using Access.Persistence;
using Contracts;
using CRM.Domain;
using CRM.Persistence;
using MasterData.Domain;
using MasterData.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Host.Tests;

[Collection(Host.Tests.Fixtures.HostIntegrationCollection.Name)]
public sealed class OpportunityEndpointsTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("fynovio_platform_host_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private WebApplicationFactory<Program>? _factory;
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // The admin (superuser) connection — used only for migrations and this test class's
        // own seeding, never for the app itself (see the runtime-role connection below). Test
        // seeding is trusted setup, same convention as CRM.Tests' PostgresFixture.CreateAdminContext.
        var connectionString = _connectionString = _container.GetConnectionString();

        // Migrate directly against the container BEFORE the WebApplicationFactory's deferred
        // host ever starts — Program.cs's own startup path (action-catalog seeding) queries
        // access.actions eagerly, and WebApplicationFactory.Services triggers that startup
        // the first time anything touches it. MasterData first: CRM's BackfillMasterDataParties
        // migration inserts into masterdata.parties, which must already exist.
        await using (var masterData = CreateMasterDataContext(connectionString))
            await masterData.Database.MigrateAsync();
        await using (var crm = CreateCrmContext(connectionString))
            await crm.Database.MigrateAsync();
        await using (var access = CreateAccessContext(connectionString))
            await access.Database.MigrateAsync();

        // The app itself must never connect as the migration superuser — superusers and table
        // owners bypass RLS unconditionally regardless of policy (AGENTS.md Database Rules),
        // which would make every non-leak/tenant-isolation claim this test class asserts
        // meaningless. Mirrors scripts/create-runtime-role.sql exactly (the production grant
        // script), extended to all three schemas the app actually touches.
        var runtimeConnectionString = await CreateRuntimeRoleAsync(connectionString);

        // Set ConnectionStrings__* env vars (ASP.NET nested config naming) — these override
        // the hardcoded localhost values in appsettings.Development.json via the framework's
        // default configuration precedence (environment variables > JSON files).
        // NOTE: mutates process-global environment variables. If a second Host.Tests test class
        // is ever added, extract this setup into a shared IAsyncLifetime fixture with
        // ICollectionFixture serialization to avoid cross-class env-var races under xUnit's
        // default parallel test-class execution.
        Environment.SetEnvironmentVariable("ConnectionStrings__Crm", runtimeConnectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__Access", runtimeConnectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__MasterData", runtimeConnectionString);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        // Legacy tests use JwtTestTokenFactory which doesn't mint sid claims — disable
        // RequireSessionClaim so these tests stay green unchanged (they predate session validation).
        Environment.SetEnvironmentVariable("Authentication__Session__RequireSessionClaim", "false");

        _factory = new WebApplicationFactory<Program>();

        // Force the host to actually start now (Program.cs's action-catalog seeding runs as
        // part of startup, before app.Run()) — PermissionSetItem.ActionKey has a real FK to
        // access.actions (PermissionSetItemConfiguration.cs), so any test that seeds a grant
        // for a real action key (e.g. "crm.opportunity.win") needs that row to already exist.
        // Without this warmup, seeding would race the lazy/deferred host startup that a
        // test's own first CreateClient() call would otherwise trigger.
        using var warmup = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Creating_an_opportunity_without_a_bearer_token_is_unauthorized()
    {
        using var client = _factory!.CreateClient();

        var response = await client.PostAsJsonAsync("/opportunities",
            new { PartyId = 1, Currency = "TRY", EstimatedAmount = 100m });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Creating_an_opportunity_for_a_tenant_the_caller_does_not_belong_to_is_forbidden()
    {
        using var client = _factory!.CreateClient();
        var token = JwtTestTokenFactory.Create("unlinked-subject", tenantId: 999_999);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync("/opportunities",
            new { PartyId = 1, Currency = "TRY", EstimatedAmount = 100m });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Authorized_caller_with_a_tenant_wide_grant_can_create_an_opportunity_over_http()
    {
        var tenantId = 5001;
        var (subject, _) = await SeedGrantAsync(tenantId, "crm.opportunity.create", relation: null, AssigneeActions);
        var partyId = await SeedPartyAsync(tenantId, "Acme");

        using var client = AuthorizedClient(subject, tenantId);
        var response = await client.PostAsJsonAsync("/opportunities",
            new { PartyId = partyId, Currency = "TRY", EstimatedAmount = 100m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("opportunityId").GetInt64() > 0);
    }

    [Fact]
    public async Task Creating_for_a_party_that_does_not_exist_is_a_422_party_not_found()
    {
        var tenantId = 5010;
        var (subject, _) = await SeedGrantAsync(tenantId, "crm.opportunity.create", relation: null);

        using var client = AuthorizedClient(subject, tenantId);
        var response = await client.PostAsJsonAsync("/opportunities",
            new { PartyId = 987_654_321L, Currency = "TRY", EstimatedAmount = 100m });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("party_not_found", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString());
    }

    /// <summary>The 2026-09-19 authorization-delta's core external claim, proven over real
    /// HTTP rather than just the unit-level CrmProblemDetailsExceptionHandlerTests: a caller
    /// who holds an owner-relation grant that doesn't cover this specific opportunity gets
    /// the exact same 404 body shape as a caller hitting a genuinely nonexistent id — never
    /// 403, never a different `type`/`title`.</summary>
    [Fact]
    public async Task Record_level_denial_on_a_mutation_is_the_same_404_as_a_genuinely_missing_opportunity()
    {
        var tenantId = 5002;
        var owner = new PrincipalRef(JwtTestTokenFactory.Issuer, $"owner-{Guid.NewGuid():N}");
        var opportunityId = await SeedOpenOpportunityAsync(tenantId, owner);
        var (callerSubject, _) = await SeedGrantAsync(tenantId, "crm.opportunity.win", relation: PermissionSetItem.OwnerRelation);

        using var client = AuthorizedClient(callerSubject, tenantId);

        var deniedResponse = await client.PostAsJsonAsync($"/opportunities/{opportunityId}/win", new { ExpectedVersion = 3 });
        var missingResponse = await client.PostAsJsonAsync("/opportunities/999999999/win", new { ExpectedVersion = 3 });

        Assert.Equal(HttpStatusCode.NotFound, deniedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);

        var deniedBody = await deniedResponse.Content.ReadFromJsonAsync<JsonElement>();
        var missingBody = await missingResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(missingBody.GetProperty("type").GetString(), deniedBody.GetProperty("type").GetString());
        Assert.Equal("not_found", deniedBody.GetProperty("type").GetString());
        // Titles necessarily differ by id (each names its own opportunity), so the identity
        // claim is that BOTH follow the exact same "Opportunity {id} was not found." shape
        // OpportunityNotFoundException produces — never the denied path's own "was denied"
        // message, which would leak that the record exists.
        Assert.Equal($"Opportunity {opportunityId} was not found.", deniedBody.GetProperty("title").GetString());
        Assert.Equal("Opportunity 999999999 was not found.", missingBody.GetProperty("title").GetString());
    }

    /// <summary>A tenant-wide grant is scoped to the caller's own tenant only — RLS means a
    /// real opportunity id from a different tenant is indistinguishable from a nonexistent
    /// one, at the HTTP boundary, without needing the authorization-delta at all (this case
    /// never reaches AccessAuthorizer — the tenant-safe load itself finds nothing).</summary>
    [Fact]
    public async Task Cross_tenant_real_opportunity_id_is_not_found_over_http()
    {
        var otherTenantId = 5003;
        var callerTenantId = 5004;
        var opportunityInOtherTenant = await SeedOpenOpportunityAsync(otherTenantId, new PrincipalRef(JwtTestTokenFactory.Issuer, "irrelevant-owner"));
        var (callerSubject, _) = await SeedGrantAsync(callerTenantId, "crm.opportunity.win", relation: null);

        using var client = AuthorizedClient(callerSubject, callerTenantId);
        var response = await client.PostAsJsonAsync($"/opportunities/{opportunityInOtherTenant}/win", new { ExpectedVersion = 3 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>PLAN.md §16's "validation errors" HTTP row: Opportunity.Create's own
    /// domain guard (a non-3-letter currency code) throws ArgumentException, mapped to
    /// 400 "validation_error" by CrmProblemDetailsExceptionHandler.</summary>
    [Fact]
    public async Task Creating_an_opportunity_with_an_invalid_currency_is_a_validation_error()
    {
        var tenantId = 5005;
        var (subject, _) = await SeedGrantAsync(tenantId, "crm.opportunity.create", relation: null, AssigneeActions);
        var partyId = await SeedPartyAsync(tenantId, "Acme");

        using var client = AuthorizedClient(subject, tenantId);
        var response = await client.PostAsJsonAsync("/opportunities",
            new { PartyId = partyId, Currency = "X", EstimatedAmount = 100m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation_error", body.GetProperty("type").GetString());
    }

    /// <summary>PLAN.md §16's "concurrency conflict" HTTP row: a stale ExpectedVersion
    /// on a real, visible-to-the-caller opportunity maps to 409 "concurrency_conflict"
    /// (distinct from the record-level-denial 404 case above — this caller genuinely
    /// can see and is authorized for this opportunity).</summary>
    [Fact]
    public async Task Winning_an_opportunity_with_a_stale_expected_version_is_a_concurrency_conflict()
    {
        var tenantId = 5006;
        var owner = new PrincipalRef(JwtTestTokenFactory.Issuer, $"owner-{Guid.NewGuid():N}");
        var opportunityId = await SeedOpenOpportunityAsync(tenantId, owner);
        var (callerSubject, _) = await SeedGrantAsync(tenantId, "crm.opportunity.win", relation: null);

        using var client = AuthorizedClient(callerSubject, tenantId);
        var response = await client.PostAsJsonAsync($"/opportunities/{opportunityId}/win", new { ExpectedVersion = 999_999 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("concurrency_conflict", body.GetProperty("type").GetString());
    }

    /// <summary>PLAN.md §16's "idempotent retry" HTTP row: AuthorizedClient fixes one
    /// Idempotency-Key header for the client's lifetime, so two identical requests on
    /// the same client are a same-key replay — the second must return the first's
    /// stored result (Replayed: true, same OpportunityId) rather than re-executing or
    /// erroring on an already-Won opportunity.</summary>
    [Fact]
    public async Task Retrying_a_win_with_the_same_idempotency_key_replays_the_first_result()
    {
        var tenantId = 5007;
        var owner = new PrincipalRef(JwtTestTokenFactory.Issuer, $"owner-{Guid.NewGuid():N}");
        var opportunityId = await SeedOpenOpportunityAsync(tenantId, owner);
        var (callerSubject, _) = await SeedGrantAsync(tenantId, "crm.opportunity.win", relation: null);

        using var client = AuthorizedClient(callerSubject, tenantId);
        var first = await client.PostAsJsonAsync($"/opportunities/{opportunityId}/win", new { ExpectedVersion = 3 });
        var second = await client.PostAsJsonAsync($"/opportunities/{opportunityId}/win", new { ExpectedVersion = 3 });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(firstBody.GetProperty("replayed").GetBoolean());
        Assert.True(secondBody.GetProperty("replayed").GetBoolean());
        Assert.Equal(firstBody.GetProperty("opportunityId").GetInt64(), secondBody.GetProperty("opportunityId").GetInt64());
    }

    /// <summary>Creates the unprivileged application role and grants it exactly what
    /// scripts/create-runtime-role.sql grants in production (crm + masterdata + identity +
    /// access schemas, evidence_records append-only) — run once, as the admin connection,
    /// against the already-migrated schema. Returns the connection string the app itself
    /// should use.</summary>
    private static async Task<string> CreateRuntimeRoleAsync(string adminConnectionString)
    {
        await using var admin = CreateCrmContext(adminConnectionString);
        await admin.Database.ExecuteSqlRawAsync("""
            CREATE ROLE fynovio_app LOGIN PASSWORD 'runtime' NOSUPERUSER NOBYPASSRLS;

            GRANT USAGE ON SCHEMA crm TO fynovio_app;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA crm TO fynovio_app;
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA crm TO fynovio_app;
            REVOKE UPDATE, DELETE ON crm.evidence_records FROM fynovio_app;

            GRANT USAGE ON SCHEMA masterdata TO fynovio_app;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA masterdata TO fynovio_app;
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA masterdata TO fynovio_app;
            REVOKE UPDATE, DELETE ON masterdata.evidence_records FROM fynovio_app;

            GRANT USAGE ON SCHEMA identity TO fynovio_app;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA identity TO fynovio_app;
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA identity TO fynovio_app;

            GRANT USAGE ON SCHEMA access TO fynovio_app;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA access TO fynovio_app;
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA access TO fynovio_app;
            REVOKE UPDATE, DELETE ON access.evidence_records FROM fynovio_app;
            """);

        var builder = new Npgsql.NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Username = "fynovio_app",
            Password = "runtime"
        };
        return builder.ConnectionString;
    }

    private HttpClient AuthorizedClient(string subject, long tenantId)
    {
        var client = _factory!.CreateClient();
        var token = JwtTestTokenFactory.Create(subject, tenantId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return client;
    }

    /// <summary>The creator becomes the owner, and an owner must be assignable (CrmAssignmentPolicy.RequiredAssigneeActions).</summary>
    private static readonly string[] AssigneeActions = ["crm.opportunity.read", "crm.opportunity.change_stage"];

    /// <summary>Seeds Account/ExternalIdentity/active TenantMembership/Role/PermissionSet/
    /// RoleAssignment/TenantAccessState granting `actionKey` (tenant-wide when `relation`
    /// is null, owner-scoped for `PermissionSetItem.OwnerRelation`). The action itself is
    /// already registered by Program.cs's own startup seeding (AccessActionCatalogSeeder,
    /// triggered the first time this WebApplicationFactory's Services are touched — see
    /// InitializeAsync's migration-ordering note) — this only adds the grant, never the
    /// action-registry row. Returns the JWT `sub` to mint a token for.</summary>
    private async Task<(string Subject, long AccountId)> SeedGrantAsync(long tenantId, string actionKey, string? relation, params string[] additionalActionKeys)
    {
        var tenant = new TenantId(tenantId);
        var subject = $"caller-{Guid.NewGuid():N}";
        var principal = new PrincipalRef(JwtTestTokenFactory.Issuer, subject);
        var suffix = "t" + Guid.NewGuid().ToString("N")[..7];

        await using var access = CreateAccessContext(_connectionString);

        var account = Account.Create($"{subject}@test.local", subject);
        access.Accounts.Add(account);
        await access.SaveChangesAsync();

        access.ExternalIdentities.Add(ExternalIdentity.Link(account.Id, principal));
        var membership = TenantMembership.Invite(tenant, account.Id);
        membership.Activate();
        access.TenantMemberships.Add(membership);

        var permissionSet = PermissionSet.Create(tenant, $"host_test_permission_set_{suffix}", "Host Test Permission Set");
        permissionSet.Grant(actionKey, relation);
        foreach (var additionalActionKey in additionalActionKeys)
            permissionSet.Grant(additionalActionKey, relation);
        access.PermissionSets.Add(permissionSet);

        var role = Role.Create(tenant, $"host_test_role_{suffix}", $"Host Test Role {suffix}");
        access.Roles.Add(role);
        await access.SaveChangesAsync();

        access.RolePermissionSets.Add(RolePermissionSet.Create(tenant, role.Id, permissionSet.Id));
        access.RoleAssignments.Add(RoleAssignment.Grant(tenant, account.Id, role.Id, account.Id, RoleAssignment.SourceBootstrap));

        if (!await access.TenantAccessStates.AnyAsync(s => s.TenantId == tenant))
            access.TenantAccessStates.Add(TenantAccessState.Initialize(tenant));

        await access.SaveChangesAsync();
        return (subject, account.Id);
    }

    private async Task<long> SeedPartyAsync(long tenantId, string name)
    {
        await using var masterData = CreateMasterDataContext(_connectionString);
        var party = Party.Create(new TenantId(tenantId), PartyType.Organization, name, null, null, null);
        masterData.Parties.Add(party);
        await masterData.SaveChangesAsync();
        return party.Id;
    }

    private async Task<long> SeedOpenOpportunityAsync(long tenantId, PrincipalRef owner)
    {
        var tenant = new TenantId(tenantId);
        var partyId = await SeedPartyAsync(tenantId, "Acme");

        await using var crm = CreateCrmContext(_connectionString);

        // Minimal published pipeline (one entry stage) so the opportunity can satisfy
        // ck_opportunities_stage_required_once_open (Task 19) once it's opened. A Guid
        // suffix keeps the name unique per (tenant_id, name) across repeated seed calls.
        var definition = PipelineDefinition.Create(tenant, $"Test Pipeline {Guid.NewGuid():N}");
        crm.PipelineDefinitions.Add(definition);
        await crm.SaveChangesAsync();
        var version = definition.AddVersion(1);
        crm.PipelineDefinitionVersions.Add(version);
        await crm.SaveChangesAsync();
        var entryStage = version.AddStage("Open", 10);
        version.Publish();
        crm.PipelineStages.Add(entryStage);
        await crm.SaveChangesAsync();

        var opportunity = Opportunity.Create(tenant, new PartyRef(tenant, partyId), owner, "TRY", 100m);
        opportunity.AddLine(new EntityRef(tenant, "masterdata", "product", 1), quantity: 1, unitPrice: 100m);
        opportunity.Open(DateTimeOffset.UtcNow.AddDays(7), version.Id, entryStage.Id);
        crm.Opportunities.Add(opportunity);
        await crm.SaveChangesAsync();
        return opportunity.Id;
    }

    private static MasterDataDbContext CreateMasterDataContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<MasterDataDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", MasterDataDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new MasterDataDbContext(options);
    }

    private static CrmDbContext CreateCrmContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CrmDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new CrmDbContext(options);
    }

    private static AccessDbContext CreateAccessContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AccessDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", AccessDbContext.AccessSchema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new RowVersionInterceptor())
            .Options;

        return new AccessDbContext(options);
    }
}
