using Access.Application.Authentication;
using Access.Domain.Authorization;
using Access.Domain.Identity;
using Access.Persistence;
using Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Xunit;

namespace Host.Tests.Fixtures;

/// <summary>Every Host integration class mutates process-global environment variables while it
/// builds its host, so they must never run in parallel with each other.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class HostIntegrationCollection
{
    public const string Name = "HostIntegration";
}

/// <summary>Sets environment variables for the duration of a host start-up and restores them.
/// ASP.NET reads configuration once when the host is built, so restoring right after start-up
/// is safe and keeps one test class's settings from leaking into the next.</summary>
internal sealed class EnvScope : IDisposable
{
    private readonly Dictionary<string, string?> _previous = new();

    private EnvScope() { }

    public static EnvScope Apply(IReadOnlyDictionary<string, string?> settings, string[] clearPrefixes)
    {
        var scope = new EnvScope();

        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var key = (string)entry.Key;
            if (clearPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal)))
                scope.Set(key, null);
        }

        foreach (var (key, value) in settings)
            scope.Set(key, value);

        return scope;
    }

    private void Set(string key, string? value)
    {
        _previous.TryAdd(key, Environment.GetEnvironmentVariable(key));
        Environment.SetEnvironmentVariable(key, value);
    }

    public void Dispose()
    {
        foreach (var (key, value) in _previous)
            Environment.SetEnvironmentVariable(key, value);
    }
}

/// <summary>A clock tests can move forward (the host's `TimeProvider` singleton is replaced by it).</summary>
internal sealed class AdvanceableTimeProvider : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>Collects every log message and structured value the host writes.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries
    {
        get
        {
            lock (_entries)
                return _entries.ToList();
        }
    }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

    public void Dispose() { }

    private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var text = formatter(state, exception);
            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
                text += " " + string.Join(' ', pairs.Select(pair => $"{pair.Key}={pair.Value}"));
            if (exception is not null)
                text += " " + exception;

            lock (owner._entries)
                owner._entries.Add(text);
        }
    }
}

/// <summary>A running host plus what tests need to talk to it.</summary>
internal sealed class AuthApiHost(WebApplicationFactory<Program> factory) : IDisposable
{
    public IServiceProvider Services => factory.Services;

    public HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public void Dispose() => factory.Dispose();
}

/// <summary>One PostgreSQL container (every module migrated, unprivileged runtime role) that
/// several host variants can be started against, plus seeding through the real Access handlers.</summary>
public sealed class AuthApiFixture : IAsyncLifetime
{
    public const string Issuer = JwtTestTokenFactory.Issuer;
    public const string Password = "Correct-Horse-Battery-9";

    private static readonly SemaphoreSlim EnvironmentGate = new(1, 1);
    private static long _nextTenantId = 50_000;

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("fynovio_auth_api_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private string _runtimeConnectionString = null!;

    public string AdminConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        AdminConnectionString = _container.GetConnectionString();

        await using (var masterData = AuthTestFixture.CreateMasterDataContext(AdminConnectionString))
            await masterData.Database.MigrateAsync();
        await using (var crm = AuthTestFixture.CreateCrmContext(AdminConnectionString))
            await crm.Database.MigrateAsync();
        await using (var access = AuthTestFixture.CreateAccessContext(AdminConnectionString))
            await access.Database.MigrateAsync();
        await using (var collaboration = AuthTestFixture.CreateCollaborationContext(AdminConnectionString))
            await collaboration.Database.MigrateAsync();

        _runtimeConnectionString = await AuthTestFixture.CreateRuntimeRoleAsync(AdminConnectionString);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Tenant ids are unique per call: all tests in a class share one database.</summary>
    public static long NewTenantId() => Interlocked.Increment(ref _nextTenantId);

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@example.test";

    /// <summary>Starts a host with the given settings (env-var style keys, e.g.
    /// `Authentication__RateLimiting__LoginPerMinute`) and optional service overrides.</summary>
    internal async Task<AuthApiHost> StartHostAsync(
        Dictionary<string, string?>? settings = null,
        Action<IServiceCollection>? configureServices = null,
        ILoggerProvider? logProvider = null)
    {
        var effective = new Dictionary<string, string?>
        {
            ["ConnectionStrings__Crm"] = _runtimeConnectionString,
            ["ConnectionStrings__Collaboration"] = _runtimeConnectionString,
            ["ConnectionStrings__Access"] = _runtimeConnectionString,
            ["ConnectionStrings__MasterData"] = _runtimeConnectionString,
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["Authentication__Session__RequireSessionClaim"] = "true",
            ["Authentication__Session__RefreshGraceSeconds"] = "1",
            // A developer's user-secrets may hold real SMTP credentials; tests must never send mail.
            ["Email__Smtp__Enabled"] = "false",
        };
        foreach (var (key, value) in settings ?? [])
            effective[key] = value;

        await EnvironmentGate.WaitAsync();
        try
        {
            using var scope = EnvScope.Apply(effective, clearPrefixes: ["Authentication__", "Email__", "Bootstrap__"]);
            var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    if (logProvider is not null)
                        services.AddLogging(logging => logging.AddProvider(logProvider));
                    configureServices?.Invoke(services);
                });
            });

            // Start the host now, while the environment variables are still in place.
            using var warmup = factory.CreateClient();
            return new AuthApiHost(factory);
        }
        finally
        {
            EnvironmentGate.Release();
        }
    }

    private AccessDbContext CreateAdminAccessContext() => AuthTestFixture.CreateAccessContext(AdminConnectionString);

    /// <summary>Creates an account with the shared test password and an ACTIVE membership in
    /// every given tenant, through the real provisioning handler.</summary>
    public async Task<SeededUser> SeedUserAsync(string? email = null, string? password = null, params long[] tenantIds)
    {
        email ??= NewEmail();
        await using var access = CreateAdminAccessContext();

        var handler = new ProvisionPasswordAccountHandler(
            access,
            new PasswordService(),
            new PasswordPolicy(new PasswordPolicyOptions()));

        var result = await handler.HandleAsync(new ProvisionPasswordAccountCommand(
            email,
            "Test User",
            password ?? Password,
            Issuer,
            tenantIds.Length > 0 ? new TenantId(tenantIds[0]) : null));

        foreach (var tenantId in tenantIds.Skip(1))
            await AddMembershipAsync(result.AccountId, tenantId, activate: true);

        return new SeededUser(email, result.AccountId, result.Principal);
    }

    public async Task AddMembershipAsync(long accountId, long tenantId, bool activate)
    {
        await using var access = CreateAdminAccessContext();
        var membership = TenantMembership.Invite(new TenantId(tenantId), accountId);
        if (activate)
            membership.Activate();
        access.TenantMemberships.Add(membership);
        await access.SaveChangesAsync();
    }

    public async Task DisableMembershipAsync(long accountId, long tenantId)
    {
        await using var access = CreateAdminAccessContext();
        var membership = await access.TenantMemberships.SingleAsync(m => m.AccountId == accountId && m.TenantId == new TenantId(tenantId));
        membership.Disable();
        await access.SaveChangesAsync();
    }

    /// <summary>Grants `actionKey` tenant-wide to the account (mirrors the legacy
    /// `OpportunityEndpointsTests.SeedGrantAsync`); the action itself is registered by the
    /// host's own start-up seeding.</summary>
    public async Task<long> GrantAsync(long accountId, long tenantId, string actionKey)
    {
        var tenant = new TenantId(tenantId);
        var suffix = "t" + Guid.NewGuid().ToString("N")[..7];
        await using var access = CreateAdminAccessContext();

        var permissionSet = PermissionSet.Create(tenant, $"auth_test_ps_{suffix}", "Auth test permission set");
        permissionSet.Grant(actionKey, null);
        access.PermissionSets.Add(permissionSet);

        var role = Role.Create(tenant, $"auth_test_role_{suffix}", $"Auth test role {suffix}");
        access.Roles.Add(role);
        await access.SaveChangesAsync();

        access.RolePermissionSets.Add(RolePermissionSet.Create(tenant, role.Id, permissionSet.Id));
        access.RoleAssignments.Add(RoleAssignment.Grant(tenant, accountId, role.Id, accountId, RoleAssignment.SourceBootstrap));

        if (!await access.TenantAccessStates.AnyAsync(s => s.TenantId == tenant))
            access.TenantAccessStates.Add(TenantAccessState.Initialize(tenant));

        await access.SaveChangesAsync();
        return role.Id;
    }

    /// <summary>Ends the account's assignment of a role a test granted (see <see cref="GrantAsync"/>), effective immediately.</summary>
    public async Task RevokeAsync(long accountId, long tenantId, long roleId)
    {
        await using var access = CreateAdminAccessContext();
        var assignments = await access.RoleAssignments
            .Where(a => a.TenantId == new TenantId(tenantId) && a.AccountId == accountId && a.RoleId == roleId)
            .ToListAsync();
        foreach (var assignment in assignments)
            assignment.Revoke();
        await access.SaveChangesAsync();
    }

    public async Task<int> LockAccountAsync(string email)
    {
        await using var access = CreateAdminAccessContext();
        return await access.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE identity.account_credentials SET locked_until = now() + interval '1 hour', failed_attempts = 5 WHERE login_email_normalized = {EmailNormalizer.Normalize(email)}");
    }

    public async Task<int> RemoveCredentialAsync(long accountId)
    {
        await using var access = CreateAdminAccessContext();
        return await access.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM identity.account_credentials WHERE account_id = {accountId}");
    }

    public async Task<(bool Revoked, string? Reason)> SessionStateAsync(Guid sessionId)
    {
        await using var access = CreateAdminAccessContext();
        var session = await access.AuthSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
        return (session.IsRevoked, session.RevokedReason);
    }

    public async Task<string?> SessionUserAgentHashAsync(Guid sessionId)
    {
        await using var access = CreateAdminAccessContext();
        return (await access.AuthSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId)).UserAgentHash;
    }

    public async Task<int> UnrotatedTokenCountAsync(Guid sessionId)
    {
        await using var access = CreateAdminAccessContext();
        return await access.RefreshTokens.CountAsync(t => t.SessionId == sessionId && t.RotatedAt == null);
    }
}

public sealed record SeededUser(string Email, long AccountId, PrincipalRef Principal);
