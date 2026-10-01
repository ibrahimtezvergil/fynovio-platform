using CRM.Persistence;
using MasterData.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SemanticCatalog.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace SemanticCatalog.Tests.Integration;

/// <summary>adr-semantic-catalog-changeset.md S-3 on an *existing* database: CRM is migrated up to the last migration before
/// the move, definitions are seeded in the old table, then the catalog migrates (copy) and CRM migrates to the tip (guarded
/// drop). Ids must survive — every id in the API and web UI stays valid — and a CRM migration that runs first must refuse
/// to lose rows rather than drop them. Each test has its own container: it manipulates migration history directly.</summary>
public sealed class FieldDefinitionDataMoveTests : IAsyncLifetime
{
    private const string PreMoveCrmMigration = "20261001140154_AddOpportunityActivityProjection";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("fynovio_platform_move_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private string Connection => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using (var masterData = new MasterDataDbContext(Options<MasterDataDbContext>(MasterDataDbContext.Schema)))
            await masterData.Database.MigrateAsync();
        await using var crm = new CrmDbContext(Options<CrmDbContext>(CrmDbContext.Schema));
        await crm.GetInfrastructure().GetRequiredService<IMigrator>().MigrateAsync(PreMoveCrmMigration);

        await ExecuteAsync(
            """
            INSERT INTO crm.tenant_field_definitions (id, tenant_id, aggregate_type, field_name, label, field_type, is_required, config, status, sort_order, owner_scope, row_version, created_at, updated_at)
            OVERRIDING SYSTEM VALUE VALUES
              (7,  5001, 'Opportunity', 'region',   'Region',   'select', true,  '{"options":[{"key":"emea","label":"EMEA","isDeprecated":false}]}'::jsonb, 'Active',     3, 'Tenant', 4, now(), now()),
              (12, 5002, 'Opportunity', 'legacy',   'Legacy',   'text',   false, '{}'::jsonb,                                                                 'Deprecated', 9, 'Tenant', 2, now(), now()),
              (13, 5001, 'Party',       'nickname', 'Nickname', 'text',   false, '{}'::jsonb,                                                                 'Active',     0, 'Tenant', 1, now(), now())
            """);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task Existing_definitions_keep_their_ids_and_the_old_table_is_dropped_after_the_copy()
    {
        await using (var semantic = new SemanticCatalogDbContext(Options<SemanticCatalogDbContext>(SemanticCatalogDbContext.Schema)))
            await semantic.Database.MigrateAsync();

        var copied = await QueryAsync("SELECT id, tenant_id, owner_context, object_type, key, label, field_type, is_required, status, sort_order, row_version FROM semantic.field_definitions ORDER BY id");
        Assert.Equal(
            [
                "7|5001|crm|opportunity|region|Region|select|True|Active|3|4",
                "12|5002|crm|opportunity|legacy|Legacy|text|False|Deprecated|9|2"
            ],
            copied);
        Assert.Equal(["emea"], await QueryAsync("SELECT config->'options'->0->>'key' FROM semantic.field_definitions WHERE id = 7"));

        // The sequence is past the copied ids, so the next definition cannot collide with one of them.
        var next = (long)(await ScalarAsync("SELECT nextval(pg_get_serial_sequence('semantic.field_definitions', 'id'))"))!;
        Assert.True(next > 12, $"next id {next} must follow the copied ids");

        await using (var crm = new CrmDbContext(Options<CrmDbContext>(CrmDbContext.Schema)))
            await crm.Database.MigrateAsync();
        Assert.Null(await ScalarAsync("SELECT to_regclass('crm.tenant_field_definitions')::text"));
    }

    [Fact]
    public async Task Migrating_crm_before_the_catalog_refuses_to_drop_definitions_it_would_lose()
    {
        await using var crm = new CrmDbContext(Options<CrmDbContext>(CrmDbContext.Schema));

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => crm.Database.MigrateAsync());

        Assert.Contains("SemanticCatalog migrations before CRM", FullMessage(failure));
        Assert.NotNull(await ScalarAsync("SELECT to_regclass('crm.tenant_field_definitions')::text"));
        Assert.Equal(3L, await ScalarAsync("SELECT count(*) FROM crm.tenant_field_definitions"));
    }

    [Fact]
    public async Task The_guard_also_refuses_when_the_catalog_lacks_a_row()
    {
        await using (var semantic = new SemanticCatalogDbContext(Options<SemanticCatalogDbContext>(SemanticCatalogDbContext.Schema)))
            await semantic.Database.MigrateAsync();
        await ExecuteAsync("DELETE FROM semantic.field_definitions WHERE id = 12");

        await using var crm = new CrmDbContext(Options<CrmDbContext>(CrmDbContext.Schema));
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => crm.Database.MigrateAsync());

        Assert.Contains("lacks", FullMessage(failure));
        Assert.NotNull(await ScalarAsync("SELECT to_regclass('crm.tenant_field_definitions')::text"));
    }

    private static string FullMessage(Exception exception) =>
        string.Join(" | ", Enumerable.Range(0, 5).Aggregate((Current: (Exception?)exception, Messages: new List<string>()),
            (state, _) => { if (state.Current is not null) state.Messages.Add(state.Current.Message); return (state.Current?.InnerException, state.Messages); }).Messages);

    private DbContextOptions<TContext> Options<TContext>(string historySchema) where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(Connection, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", historySchema))
            .UseSnakeCaseNamingConvention()
            .Options;

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Connection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Connection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        return value is DBNull ? null : value;
    }

    private async Task<List<string>> QueryAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Connection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync())
            rows.Add(string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetValue(i))));
        return rows;
    }
}
