using Collaboration.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Collaboration.Tests.Integration;

/// <summary>FF03 (doc 12) enforcement for Collaboration's tenant-scoped tables, exercised as the unprivileged
/// runtime role (`fynovio_app`, created from the real scripts/create-runtime-role.sql). For every table:
/// RLS is enabled and forced, another tenant's rows are invisible and immutable, a write for another tenant is
/// rejected by WITH CHECK (SQLSTATE 42501), and an unset tenant context fails closed — including on a connection
/// that previously ran a tenant transaction (where the GUC is '' rather than NULL and the policy's NULLIF matters).</summary>
[Collection(nameof(PostgresCollection))]
public sealed class RowLevelSecurityTests(PostgresFixture fixture)
{
    private const string InsufficientPrivilege = "42501";

    public static TheoryData<string> Tables => new() { "calendar_entries", "idempotency_records", "outbox_messages" };

    /// <summary>One valid INSERT per table, parameterised on `@t` (tenant) and `@k` (a unique key).</summary>
    private static readonly Dictionary<string, string> InsertSql = new()
    {
        ["calendar_entries"] = """
            INSERT INTO collaboration.calendar_entries
                (tenant_id, owner_principal_issuer, owner_principal_subject, title, color, all_day, start_at, row_version, created_at, updated_at)
            VALUES (@t, 'issuer', 'subject', 'Title', '#336699', false, now(), 1, now(), now())
            """,
        ["idempotency_records"] = """
            INSERT INTO collaboration.idempotency_records
                (tenant_id, principal_issuer, principal_subject, operation, idempotency_key, request_hash, response_status, response_payload, created_at, expires_at)
            VALUES (@t, 'issuer', 'subject', 'Op', @k, 'hash', 201, '{}'::jsonb, now(), now() + interval '1 day')
            """,
        ["outbox_messages"] = """
            INSERT INTO collaboration.outbox_messages
                (tenant_id, aggregate_type, aggregate_id, aggregate_version, event_id, event_type, source, subject, correlation_id, payload, occurred_at)
            VALUES (@t, 'CalendarEntry', 1, 1, gen_random_uuid(), 'type', '/source', 'subject/1', gen_random_uuid(), '{}'::jsonb, now())
            """
    };

    [Fact]
    public async Task Every_collaboration_table_has_row_level_security_enabled_forced_and_policied()
    {
        await using var connection = new NpgsqlConnection(fixture.AdminConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.relname,
                   c.relrowsecurity,
                   c.relforcerowsecurity,
                   EXISTS (
                       SELECT 1 FROM pg_policies p
                       WHERE p.schemaname = 'collaboration' AND p.tablename = c.relname AND p.policyname = 'tenant_isolation'
                         AND p.qual LIKE '%NULLIF%' AND p.with_check LIKE '%NULLIF%'
                   ) AS has_policy
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'collaboration' AND c.relkind = 'r' AND c.relname <> '__ef_migrations_history';
            """;

        var tables = new List<string>();
        var uncovered = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
            if (!reader.GetBoolean(1) || !reader.GetBoolean(2) || !reader.GetBoolean(3))
                uncovered.Add($"{reader.GetString(0)} (enabled={reader.GetBoolean(1)}, forced={reader.GetBoolean(2)}, policied={reader.GetBoolean(3)})");
        }

        Assert.Equal(new[] { "calendar_entries", "idempotency_records", "outbox_messages" }, tables.Order());
        Assert.True(uncovered.Count == 0, $"Tables missing full RLS coverage: {string.Join(", ", uncovered)}");
    }

    [Fact]
    public async Task The_runtime_role_is_not_a_superuser_and_does_not_bypass_rls()
    {
        await using var connection = await OpenRuntimeAsync();
        await using var command = new NpgsqlCommand(
            "SELECT rolsuper, rolbypassrls FROM pg_roles WHERE rolname = current_user", connection);
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.False(reader.GetBoolean(0));
        Assert.False(reader.GetBoolean(1));
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public async Task Rows_of_another_tenant_are_invisible(string table)
    {
        var tenantA = TestData.NextTenant();
        var tenantB = TestData.NextTenant();
        await InsertAsAdminAsync(table, tenantA);

        Assert.Equal(1, await CountUnderTenantAsync(table, sessionTenant: tenantA, rowTenant: tenantA));
        Assert.Equal(0, await CountUnderTenantAsync(table, sessionTenant: tenantB, rowTenant: tenantA));
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public async Task A_write_for_another_tenant_is_rejected_by_with_check(string table)
    {
        var sessionTenant = TestData.NextTenant();
        var foreignTenant = TestData.NextTenant();
        await using var connection = await OpenRuntimeAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantAsync(connection, transaction, sessionTenant);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(connection, transaction, table, foreignTenant));

        Assert.Equal(InsufficientPrivilege, ex.SqlState);
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public async Task A_write_for_the_session_tenant_succeeds_so_the_rejection_above_is_about_the_tenant(string table)
    {
        var tenant = TestData.NextTenant();
        await using var connection = await OpenRuntimeAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantAsync(connection, transaction, tenant);

        await InsertAsync(connection, transaction, table, tenant);
        await transaction.CommitAsync();

        Assert.Equal(1, await CountUnderTenantAsync(table, tenant, tenant));
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public async Task Another_tenants_rows_cannot_be_updated_or_deleted(string table)
    {
        var tenantA = TestData.NextTenant();
        var tenantB = TestData.NextTenant();
        await InsertAsAdminAsync(table, tenantA);
        await using var connection = await OpenRuntimeAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantAsync(connection, transaction, tenantB);

        await using var update = new NpgsqlCommand($"UPDATE collaboration.{table} SET tenant_id = tenant_id WHERE tenant_id = @t", connection, transaction);
        update.Parameters.AddWithValue("t", tenantA.Value);
        await using var delete = new NpgsqlCommand($"DELETE FROM collaboration.{table} WHERE tenant_id = @t", connection, transaction);
        delete.Parameters.AddWithValue("t", tenantA.Value);

        Assert.Equal(0, await update.ExecuteNonQueryAsync());
        Assert.Equal(0, await delete.ExecuteNonQueryAsync());
        await transaction.RollbackAsync();
        Assert.Equal(1, await CountUnderTenantAsync(table, tenantA, tenantA));
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public async Task A_connection_with_no_tenant_context_sees_nothing_and_cannot_write(string table)
    {
        var tenant = TestData.NextTenant();
        await InsertAsAdminAsync(table, tenant);
        await using var connection = await OpenRuntimeAsync();

        await using (var transaction = await connection.BeginTransactionAsync())
        {
            Assert.Equal(0, await CountAsync(connection, transaction, table));
            var ex = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(connection, transaction, table, tenant));
            Assert.Equal(InsufficientPrivilege, ex.SqlState);
        }
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public async Task A_connection_that_previously_ran_a_tenant_transaction_fails_closed_without_a_cast_error(string table)
    {
        var tenant = TestData.NextTenant();
        await InsertAsAdminAsync(table, tenant);
        await using var connection = await OpenRuntimeAsync();

        await using (var first = await connection.BeginTransactionAsync())
        {
            await SetTenantAsync(connection, first, tenant);
            Assert.Equal(1, await CountAsync(connection, first, table));
            await first.CommitAsync();
        }

        // The same physical connection now reports '' (not NULL) for the transaction-local GUC.
        await using var probe = new NpgsqlCommand("SELECT current_setting('app.tenant_id', true)", connection);
        Assert.Equal(string.Empty, (string?)await probe.ExecuteScalarAsync());

        await using var second = await connection.BeginTransactionAsync();
        Assert.Equal(0, await CountAsync(connection, second, table)); // ''::bigint would have raised 22P02
    }

    [Fact]
    public async Task SetTenantContextAsync_outside_a_transaction_throws()
    {
        await using var context = PostgresFixture.CreateContext(await fixture.RuntimeConnectionStringAsync());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SetTenantContextAsync(TestData.NextTenant()));

        Assert.Contains("transaction", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetTenantContextAsync_inside_a_transaction_scopes_the_tenant_to_that_transaction_only()
    {
        var tenant = TestData.NextTenant();
        await using var context = PostgresFixture.CreateContext(await fixture.RuntimeConnectionStringAsync());
        await context.Database.OpenConnectionAsync();

        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            await context.SetTenantContextAsync(tenant);
            Assert.Equal(tenant.Value.ToString(), await CurrentSettingAsync(context));
            await transaction.CommitAsync();
        }

        Assert.Equal(string.Empty, await CurrentSettingAsync(context));
    }

    private static async Task<string?> CurrentSettingAsync(CollaborationDbContext context)
    {
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await using var command = new NpgsqlCommand("SELECT current_setting('app.tenant_id', true)", connection);
        return (string?)await command.ExecuteScalarAsync();
    }

    private async Task<NpgsqlConnection> OpenRuntimeAsync()
    {
        var connection = new NpgsqlConnection(await fixture.RuntimeConnectionStringAsync());
        await connection.OpenAsync();
        return connection;
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant)
    {
        await using var command = new NpgsqlCommand("SELECT set_config('app.tenant_id', @t, true)", connection, transaction);
        command.Parameters.AddWithValue("t", tenant.Value.ToString());
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string table, TenantId tenant)
    {
        await using var command = new NpgsqlCommand(InsertSql[table], connection, transaction);
        command.Parameters.AddWithValue("t", tenant.Value);
        if (InsertSql[table].Contains("@k"))
            command.Parameters.AddWithValue("k", Guid.NewGuid().ToString());
        await command.ExecuteNonQueryAsync();
    }

    private async Task InsertAsAdminAsync(string table, TenantId tenant)
    {
        await using var connection = new NpgsqlConnection(fixture.AdminConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await InsertAsync(connection, transaction, table, tenant);
        await transaction.CommitAsync();
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string table)
    {
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM collaboration.{table}", connection, transaction);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<long> CountUnderTenantAsync(string table, TenantId sessionTenant, TenantId rowTenant)
    {
        await using var connection = await OpenRuntimeAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetTenantAsync(connection, transaction, sessionTenant);
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM collaboration.{table} WHERE tenant_id = @t", connection, transaction);
        command.Parameters.AddWithValue("t", rowTenant.Value);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
