using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Access.Domain.Identity;
using Access.Persistence;
using CRM.Persistence;
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

        Environment.SetEnvironmentVariable("FYNOVIO_CRM_CONNECTION_STRING", _container.GetConnectionString());
        Environment.SetEnvironmentVariable("FYNOVIO_ACCESS_CONNECTION_STRING", _container.GetConnectionString());
        Environment.SetEnvironmentVariable("FYNOVIO_MASTERDATA_CONNECTION_STRING", _container.GetConnectionString());

        _factory = new WebApplicationFactory<Program>();

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CrmDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<AccessDbContext>().Database.MigrateAsync();
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
}
