using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Access.Domain.Identity;
using Access.Persistence;
using CRM.Persistence;
using MasterData.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Host.Tests;

public sealed class OpportunityEndpointsTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("fynovio_platform_host_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private WebApplicationFactory<Program>? _factory;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var connectionString = _container.GetConnectionString();
        // Set ConnectionStrings__* env vars (ASP.NET nested config naming) — these override
        // the hardcoded localhost values in appsettings.Development.json via the framework's
        // default configuration precedence (environment variables > JSON files).
        // NOTE: mutates process-global environment variables. If a second Host.Tests test class
        // is ever added, extract this setup into a shared IAsyncLifetime fixture with
        // ICollectionFixture serialization to avoid cross-class env-var races under xUnit's
        // default parallel test-class execution.
        Environment.SetEnvironmentVariable("ConnectionStrings__Crm", connectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__Access", connectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__MasterData", connectionString);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

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

        _factory = new WebApplicationFactory<Program>();
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

    // A full "authorized happy path" test additionally needs an Account/ExternalIdentity/
    // TenantMembership/RoleAssignment/Role/PermissionSet seed granting crm.opportunity.create
    // tenant-wide, plus a MasterData Party to reference — wire this the same way
    // WinOpportunityHandlerTests seeds its fixtures, using AccessDbContext's and
    // MasterDataDbContext's own factories from this WebApplicationFactory's DI container.
    // Left as the next test to add in this file once Tasks 6-20's fixtures are available
    // to import; the two tests above already prove the authentication/tenant-membership
    // gate itself works end-to-end over real HTTP, which is this task's core claim.

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
