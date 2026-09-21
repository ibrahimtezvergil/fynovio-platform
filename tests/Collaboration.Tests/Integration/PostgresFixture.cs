using Collaboration.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Reflection;
using System.Text.RegularExpressions;
using Testcontainers.PostgreSql;
using Xunit;

namespace Collaboration.Tests.Integration;

/// <summary>Standalone Testcontainers PostgreSQL fixture for Collaboration module tests.
/// Creates a database literally named `fynovio_platform` (must match scripts/create-runtime-role.sql),
/// runs ONLY Collaboration migrations, and extracts the Collaboration section from the actual
/// create-runtime-role.sql to bootstrap the unprivileged runtime role (FF03).</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("fynovio_platform")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly SemaphoreSlim _runtimeRoleGate = new(1, 1);
    private string? _runtimeConnectionString;

    public string AdminConnectionString => _container.GetConnectionString();

    /// <summary>Migration-running superuser (bypasses RLS). Use CreateAdminContext() for admin work.
    /// Runtime role: separate, unprivileged, subject to RLS (FF03). Use RuntimeConnectionStringAsync()
    /// to get the connection string for isolation tests. The role is created once; concurrent first
    /// callers wait on the gate instead of each running the script (CREATE ROLE is not idempotent).</summary>
    public async Task<string> RuntimeConnectionStringAsync()
    {
        await _runtimeRoleGate.WaitAsync();
        try
        {
            if (_runtimeConnectionString is not null)
                return _runtimeConnectionString;

            var scriptContent = LoadAndExtractCollaborationScript();
            var password = ExtractRuntimePassword(scriptContent);

            await using (var context = CreateAdminContext())
            {
                await context.Database.ExecuteSqlRawAsync(scriptContent);
            }

            var builder = new NpgsqlConnectionStringBuilder(AdminConnectionString)
            {
                Username = "fynovio_app",
                Password = password
            };

            _runtimeConnectionString = builder.ConnectionString;
            return _runtimeConnectionString;
        }
        finally
        {
            _runtimeRoleGate.Release();
        }
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateAdminContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public CollaborationDbContext CreateAdminContext() => CreateContext(AdminConnectionString);
    public CollaborationDbContext CreateCollaborationContext() => CreateContext(AdminConnectionString);
    public static CollaborationDbContext CreateCollaborationContext(string connectionString) => CreateContext(connectionString);

    public static CollaborationDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<CollaborationDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CollaborationDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new CollaborationDbContext(options);
    }

    /// <summary>Locate the repo root by walking up from AppContext.BaseDirectory until
    /// fynovio-platform.slnx is found. Fail loudly if not found (file corruption or misconfiguration).</summary>
    private static string LocateRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "fynovio-platform.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        throw new InvalidOperationException(
            $"Repository root not found (fynovio-platform.slnx not found walking up from {AppContext.BaseDirectory}).");
    }

    /// <summary>Load scripts/create-runtime-role.sql from the repo root and extract:
    /// (a) the header up to and including `GRANT CONNECT ON DATABASE fynovio_platform`,
    /// and (b) the Collaboration section (from `-- Collaboration module.` to right before
    /// `-- Identity + Access modules`). Fail loudly if section markers are missing.</summary>
    private static string LoadAndExtractCollaborationScript()
    {
        var repoRoot = LocateRepositoryRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "create-runtime-role.sql");

        if (!File.Exists(scriptPath))
            throw new InvalidOperationException($"Script not found: {scriptPath}");

        var lines = File.ReadAllLines(scriptPath);

        // Find the end of the header (GRANT CONNECT ON DATABASE fynovio_platform)
        var grantConnectIndex = Array.FindIndex(lines,
            l => l.Contains("GRANT CONNECT ON DATABASE fynovio_platform", StringComparison.Ordinal));
        if (grantConnectIndex < 0)
            throw new InvalidOperationException("Script marker not found: GRANT CONNECT ON DATABASE fynovio_platform");

        // Find the Collaboration section start
        var collaborationStartIndex = Array.FindIndex(lines,
            l => l.StartsWith("-- Collaboration module.", StringComparison.Ordinal));
        if (collaborationStartIndex < 0)
            throw new InvalidOperationException("Script marker not found: -- Collaboration module.");

        // Find the Identity + Access section start (marks the end of Collaboration section)
        var identityAccessStartIndex = Array.FindIndex(lines, collaborationStartIndex + 1,
            l => l.StartsWith("-- Identity + Access modules", StringComparison.Ordinal));
        if (identityAccessStartIndex < 0)
            throw new InvalidOperationException("Script marker not found: -- Identity + Access modules");

        // Extract header up to GRANT CONNECT, plus the Collaboration section
        var headerLines = lines[..(grantConnectIndex + 1)];
        var collaborationLines = lines[collaborationStartIndex..identityAccessStartIndex];

        // Combine and return as SQL text
        var combined = headerLines.Concat(collaborationLines).ToArray();
        return string.Join("\n", combined);
    }

    /// <summary>Extract the password from `CREATE ROLE fynovio_app LOGIN PASSWORD '...'`.</summary>
    private static string ExtractRuntimePassword(string scriptContent)
    {
        var match = Regex.Match(scriptContent, @"CREATE ROLE fynovio_app LOGIN PASSWORD '([^']+)'");
        if (!match.Success)
            throw new InvalidOperationException("Could not extract password from script (malformed CREATE ROLE line).");
        return match.Groups[1].Value;
    }
}
