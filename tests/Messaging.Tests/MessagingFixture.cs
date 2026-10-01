using Access.Persistence;
using Collaboration.Persistence;
using CRM.Persistence;
using MasterData.Persistence;
using SemanticCatalog.Persistence;
using Messaging.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TenantLifecycle.Persistence;
using Testcontainers.PostgreSql;

namespace Messaging.Tests;

/// <summary>Every module schema plus <c>messaging</c>, migrated in the production order (Messaging last), with the
/// roles created by the real <c>scripts/*.sql</c> — so a grant missing from a script fails here. The container's
/// <c>postgres</c> user is a superuser and bypasses RLS: use it only to seed and inspect, never as the code under
/// test (adr-event-consumption.md, E-6 test rule).</summary>
public sealed class MessagingFixture : IAsyncLifetime
{
    public const string RelayPassword = "relay";
    public const string RuntimePassword = "runtime";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("fynovio_platform")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string AdminConnectionString => _container.GetConnectionString();
    public string RelayConnectionString => As("fynovio_relay", RelayPassword);
    public string RuntimeConnectionString => As("fynovio_app", RuntimePassword);

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await Migrate(new MasterDataDbContext(Options<MasterDataDbContext>(MasterDataDbContext.Schema)));
        // SemanticCatalog before CRM (adr-semantic-catalog-changeset.md S-3).
        await Migrate(new SemanticCatalogDbContext(Options<SemanticCatalogDbContext>(SemanticCatalogDbContext.Schema)));
        await Migrate(new CrmDbContext(Options<CrmDbContext>(CrmDbContext.Schema)));
        await Migrate(new AccessDbContext(Options<AccessDbContext>(AccessDbContext.AccessSchema)));
        await Migrate(new CollaborationDbContext(Options<CollaborationDbContext>(CollaborationDbContext.Schema)));
        await Migrate(new TenantLifecycleDbContext(Options<TenantLifecycleDbContext>(TenantLifecycleDbContext.Schema)));
        await Migrate(MessagingDbContextFactory.Create(AdminConnectionString));

        await RunScriptAsync("create-runtime-role.sql", RuntimePassword);
        await RunScriptAsync("create-relay-role.sql", RelayPassword);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public NpgsqlDataSource Admin() => NpgsqlDataSource.Create(AdminConnectionString);

    /// <summary>Seeds one outbox row as the migration role and returns its id and event id.</summary>
    public async Task<(long Id, Guid EventId)> InsertOutboxAsync(
        string schema, long tenantId, string eventType, long aggregateId = 1, long version = 1,
        string aggregateType = "Thing", string payload = """{"secret":"payload"}""", DateTimeOffset? occurredAt = null)
    {
        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        var eventId = Guid.NewGuid();
        await using var command = new NpgsqlCommand(
            $"""
            INSERT INTO {schema}.outbox_messages
                (tenant_id, aggregate_type, aggregate_id, aggregate_version, event_id, event_type, source, subject, correlation_id, causation_id, payload, occurred_at)
            VALUES (@tenant, @aggregateType, @aggregateId, @version, @eventId, @type, '/test', 'things/1', @correlation, @causation, @payload::jsonb, @occurred)
            RETURNING id
            """,
            connection);
        command.Parameters.AddWithValue("tenant", tenantId);
        command.Parameters.AddWithValue("aggregateType", aggregateType);
        command.Parameters.AddWithValue("aggregateId", aggregateId);
        command.Parameters.AddWithValue("version", version);
        command.Parameters.AddWithValue("eventId", eventId);
        command.Parameters.AddWithValue("type", eventType);
        command.Parameters.AddWithValue("correlation", Guid.NewGuid());
        command.Parameters.AddWithValue("causation", Guid.NewGuid());
        command.Parameters.AddWithValue("payload", payload);
        command.Parameters.AddWithValue("occurred", occurredAt ?? DateTimeOffset.UtcNow);
        var id = (long)(await command.ExecuteScalarAsync())!;
        return (id, eventId);
    }

    public async Task<T?> AdminScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? default : (T)result;
    }

    public async Task AdminExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private string As(string user, string password) =>
        new NpgsqlConnectionStringBuilder(AdminConnectionString) { Username = user, Password = password }.ConnectionString;

    private DbContextOptions<TContext> Options<TContext>(string historySchema) where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(AdminConnectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", historySchema))
            .UseSnakeCaseNamingConvention()
            .Options;

    private static async Task Migrate(DbContext context)
    {
        await using (context)
            await context.Database.MigrateAsync();
    }

    private async Task RunScriptAsync(string name, string password)
    {
        var sql = (await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "scripts", name))).Replace("change-me", password);
        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "fynovio-platform.slnx")))
                return directory.FullName;
        }
        throw new InvalidOperationException("Repository root not found.");
    }
}

[CollectionDefinition(Name)]
public sealed class MessagingCollection : ICollectionFixture<MessagingFixture>
{
    public const string Name = "messaging";
}
