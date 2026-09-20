using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Access.Application.Authentication;
using Access.Domain.Authentication;
using Access.Domain.Identity;
using Access.Persistence;
using Contracts;
using Host.Authentication;
using Host.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Host.Tests.Authentication;

public sealed class AuthEndpointsTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("fynovio_auth_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private WebApplicationFactory<Program>? _factory;
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var connectionString = _connectionString = _container.GetConnectionString();

        // Migrate
        await using (var masterData = AuthTestFixture.CreateMasterDataContext(connectionString))
            await masterData.Database.MigrateAsync();
        await using (var crm = AuthTestFixture.CreateCrmContext(connectionString))
            await crm.Database.MigrateAsync();
        await using (var access = AuthTestFixture.CreateAccessContext(connectionString))
            await access.Database.MigrateAsync();

        var runtimeConnectionString = await AuthTestFixture.CreateRuntimeRoleAsync(connectionString);

        Environment.SetEnvironmentVariable("ConnectionStrings__Crm", runtimeConnectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__Access", runtimeConnectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__MasterData", runtimeConnectionString);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("Authentication__Session__RequireSessionClaim", "true");
        Environment.SetEnvironmentVariable("Authentication__AllowedOrigins__0", "http://localhost:5173");
        Environment.SetEnvironmentVariable("Authentication__RateLimiting__LoginPerMinute", "5");

        _factory = new WebApplicationFactory<Program>();
        using var warmup = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task GetConfig_returns_password_policy()
    {
        using var client = _factory!.CreateClient();
        var response = await client.GetAsync("/auth/config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("selfRegistrationEnabled").GetBoolean());
        Assert.True(body.TryGetProperty("passwordPolicy", out var policy));
        AssertNoStoreHeader(response);
    }

    [Fact]
    public async Task LoginSingleTenant_returns_authenticated_with_token()
    {
        var account = await SeedAccountAsync(1);
        using var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "fynovio");

        var response = await client.PostAsJsonAsync("/auth/login",
            new { email = account.Email, password = "TestPassword123!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("authenticated", body.GetProperty("status").GetString());
        Assert.True(body.TryGetProperty("accessToken", out _));

        var setCookie = GetSetCookieHeader(response);
        Assert.NotNull(setCookie);
        Assert.Contains("HttpOnly", setCookie);
        Assert.Contains("Secure", setCookie);
        Assert.Contains("SameSite=Strict", setCookie);
        AssertNoStoreHeader(response);
    }

    [Fact]
    public async Task LoginMultiTenant_returns_tenant_selection_required()
    {
        await SeedAccountAsync(2);
        using var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "fynovio");

        var email = $"test-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/auth/login",
            new { email, password = "TestPassword123!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("tenant_selection_required", body.GetProperty("status").GetString());
        Assert.False(body.TryGetProperty("accessToken", out _));
    }

    [Fact]
    public async Task LoginZeroMemberships_returns_no_membership()
    {
        await SeedAccountAsync(0);
        using var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "fynovio");

        var email = $"test-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/auth/login",
            new { email, password = "TestPassword123!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("no_membership", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task LoginUnknownEmail_returns_401_invalid_credentials()
    {
        using var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "fynovio");

        var response = await client.PostAsJsonAsync("/auth/login",
            new { email = "unknown@example.com", password = "Password123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginWrongPassword_returns_401_identical_to_unknown()
    {
        var account = await SeedAccountAsync(1);
        using var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "fynovio");

        var response = await client.PostAsJsonAsync("/auth/login",
            new { email = account.Email, password = "WrongPassword123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginValidationError_returns_400()
    {
        using var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "fynovio");

        var response = await client.PostAsJsonAsync("/auth/login",
            new { email = "", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LoginMissingCsrfHeader_returns_403()
    {
        using var client = _factory!.CreateClient();
        // No X-Requested-With header

        var response = await client.PostAsJsonAsync("/auth/login",
            new { email = "test@example.com", password = "Password123!" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LoginRateLimitExceeded_returns_429()
    {
        var account = await SeedAccountAsync(1);
        using var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "fynovio");

        // Try 5 times (limit is 5)
        for (int i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/auth/login",
                new { email = account.Email, password = "Wrong" });
        }

        var response = await client.PostAsJsonAsync("/auth/login",
            new { email = account.Email, password = "Wrong" });

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Logout_returns_204_and_clears_cookie()
    {
        using var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "fynovio");

        var response = await client.PostAsync("/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var setCookie = GetSetCookieHeader(response);
        Assert.NotNull(setCookie);
    }

    [Fact]
    public async Task GetMe_requires_token()
    {
        using var client = _factory!.CreateClient();

        var response = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static void AssertNoStoreHeader(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Cache-Control", out var values));
        Assert.Contains("no-store", values.First());
    }

    private static string? GetSetCookieHeader(HttpResponseMessage response)
    {
        return response.Headers.Where(h => h.Key == "Set-Cookie")
            .SelectMany(h => h.Value)
            .FirstOrDefault();
    }

    private async Task<Account> SeedAccountAsync(int membershipCount)
    {
        await using var access = AuthTestFixture.CreateAccessContext(_connectionString);

        var email = $"test-{Guid.NewGuid():N}@example.com";
        var account = Account.Create(email, "Test User", "en-US");
        access.Accounts.Add(account);
        await access.SaveChangesAsync();

        var principal = new PrincipalRef("https://fynovio.local", account.Id.ToString());
        access.ExternalIdentities.Add(ExternalIdentity.Link(account.Id, principal));

        var passwordService = new PasswordService();
        var credential = AccountCredential.Create(account.Id, email, passwordService.HashPassword("TestPassword123!"));
        access.AccountCredentials.Add(credential);

        for (int i = 0; i < membershipCount; i++)
        {
            var tenantId = new TenantId(5000 + i);
            var membership = TenantMembership.Invite(tenantId, account.Id);
            membership.Activate();
            access.TenantMemberships.Add(membership);
        }

        await access.SaveChangesAsync();
        return account;
    }
}
