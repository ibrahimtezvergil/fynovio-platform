using Access.Domain.Identity;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Access.Tests.Integration.Authentication;

internal static class AuthTestSql
{
    private static long _nextTenantBlock = 1000;

    /// <summary>Tests in a class share one container, so each test seeds its own accounts/tenants.</summary>
    public static long NextTenantBlock() => Interlocked.Add(ref _nextTenantBlock, 10);

    public static async Task<object?> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync();
    }

    public static string RepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "fynovio-platform.slnx")))
            directory = directory.Parent;

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Repository root not found."), relativePath);
    }
}

/// <summary>The security-critical part of the authentication foundation: `membership_self_view`
/// must expose ONLY the authenticated account's own membership rows, read-only, and must leave
/// the `tenant_isolation` policy behaviour untouched. Always the unprivileged runtime role —
/// superusers bypass RLS (see <see cref="AccessRlsTests"/>).</summary>
public sealed class MembershipSelfViewRlsTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public MembershipSelfViewRlsTests(PostgresFixture fixture) => _fixture = fixture;

    private sealed record Seed(long AccountA, long AccountB, TenantId Tenant1, TenantId Tenant2);

    /// <summary>A is an active member of tenant 1 and 2; B only of tenant 2.</summary>
    private async Task<Seed> SeedAsync()
    {
        var block = AuthTestSql.NextTenantBlock();
        var tenant1 = new TenantId(block + 1);
        var tenant2 = new TenantId(block + 2);

        await using var admin = _fixture.CreateAdminContext();
        var accountA = Account.Create($"a{block}@example.com", "Account A");
        var accountB = Account.Create($"b{block}@example.com", "Account B");
        admin.Accounts.AddRange(accountA, accountB);
        await admin.SaveChangesAsync();

        foreach (var (tenant, account) in new[] { (tenant1, accountA.Id), (tenant2, accountA.Id), (tenant2, accountB.Id) })
        {
            var membership = TenantMembership.Invite(tenant, account);
            membership.Activate();
            admin.TenantMemberships.Add(membership);
        }

        await admin.SaveChangesAsync();
        return new Seed(accountA.Id, accountB.Id, tenant1, tenant2);
    }

    private async Task<AccessDbContext> RuntimeContextAsync() =>
        PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());

    private static async Task<List<(long AccountId, long TenantId)>> VisibleAsync(AccessDbContext context) =>
        (await context.TenantMemberships.ToListAsync()).Select(m => (m.AccountId, m.TenantId.Value)).ToList();

    [Fact]
    public async Task Account_context_shows_only_that_accounts_memberships_across_tenants()
    {
        var seed = await SeedAsync();
        await using var runtime = await RuntimeContextAsync();
        await using var transaction = await runtime.Database.BeginTransactionAsync();
        await runtime.SetAccountContextAsync(seed.AccountA); // deliberately NO tenant context

        var visible = await VisibleAsync(runtime);

        Assert.Contains((seed.AccountA, seed.Tenant1.Value), visible);
        Assert.Contains((seed.AccountA, seed.Tenant2.Value), visible);
        Assert.All(visible, row => Assert.Equal(seed.AccountA, row.AccountId));
        Assert.DoesNotContain(visible, row => row.AccountId == seed.AccountB);
    }

    [Fact]
    public async Task No_context_at_all_shows_no_memberships()
    {
        await SeedAsync();
        await using var runtime = await RuntimeContextAsync();
        await using var transaction = await runtime.Database.BeginTransactionAsync();

        Assert.Empty(await VisibleAsync(runtime));
    }

    [Fact]
    public async Task Account_context_grants_no_write_path()
    {
        var seed = await SeedAsync();
        await using var runtime = await RuntimeContextAsync();
        await using var transaction = await runtime.Database.BeginTransactionAsync();
        await runtime.SetAccountContextAsync(seed.AccountA);

        // INSERT for a tenant the account is not a member of: rejected by tenant_isolation's WITH CHECK.
        runtime.TenantMemberships.Add(TenantMembership.Invite(new TenantId(seed.Tenant1.Value + 5), seed.AccountA));
        var insertFailure = await Assert.ThrowsAsync<DbUpdateException>(() => runtime.SaveChangesAsync());
        Assert.Equal("42501", Assert.IsType<PostgresException>(insertFailure.InnerException).SqlState);
        runtime.ChangeTracker.Clear();
        await transaction.RollbackAsync();

        // UPDATE / DELETE: the self-view policy is SELECT-only, so the rows are not writable → 0 affected.
        await using var second = await RuntimeContextAsync();
        await using var secondTransaction = await second.Database.BeginTransactionAsync();
        await second.SetAccountContextAsync(seed.AccountA);
        var updated = await second.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE identity.tenant_memberships SET disabled_at = now() WHERE account_id = {seed.AccountA}");
        var deleted = await second.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM identity.tenant_memberships WHERE account_id = {seed.AccountA}");
        await secondTransaction.CommitAsync();

        Assert.Equal(0, updated);
        Assert.Equal(0, deleted);

        await using var admin = _fixture.CreateAdminContext();
        var intact = await admin.TenantMemberships.Where(m => m.AccountId == seed.AccountA).ToListAsync();
        Assert.Equal(2, intact.Count);
        Assert.All(intact, m => Assert.Null(m.DisabledAt));
    }

    [Fact]
    public async Task Tenant_isolation_behaviour_is_unchanged_by_the_self_view_policy()
    {
        var seed = await SeedAsync();
        await using var runtime = await RuntimeContextAsync();
        await using var transaction = await runtime.Database.BeginTransactionAsync();
        await runtime.SetTenantContextAsync(seed.Tenant2);

        var visible = await VisibleAsync(runtime);

        // Tenant 2 shows both members of tenant 2 and nothing of tenant 1.
        Assert.Contains((seed.AccountA, seed.Tenant2.Value), visible);
        Assert.Contains((seed.AccountB, seed.Tenant2.Value), visible);
        Assert.DoesNotContain(visible, row => row.TenantId == seed.Tenant1.Value);
    }

    [Fact]
    public async Task Account_context_is_transaction_local_and_does_not_leak_to_the_next_transaction()
    {
        var seed = await SeedAsync();
        await using var runtime = await RuntimeContextAsync();

        await using (var first = await runtime.Database.BeginTransactionAsync())
        {
            await runtime.SetAccountContextAsync(seed.AccountA);
            Assert.NotEmpty(await VisibleAsync(runtime));
            await first.CommitAsync();
        }

        await using var second = await runtime.Database.BeginTransactionAsync(); // same context/connection
        Assert.Empty(await VisibleAsync(runtime));
    }

    [Fact]
    public async Task SetAccountContext_requires_an_explicit_transaction()
    {
        await using var runtime = await RuntimeContextAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.SetAccountContextAsync(1));
    }
}

/// <summary>Privilege matrix of the runtime role on the authentication tables and the real
/// permission failure for the append-only audit log.</summary>
public sealed class AuthTablePermissionTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public AuthTablePermissionTests(PostgresFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("account_credentials", "SELECT", true)]
    [InlineData("account_credentials", "INSERT", true)]
    [InlineData("account_credentials", "UPDATE", true)]
    [InlineData("account_credentials", "DELETE", true)]
    [InlineData("auth_sessions", "SELECT", true)]
    [InlineData("auth_sessions", "INSERT", true)]
    [InlineData("auth_sessions", "UPDATE", true)]
    [InlineData("auth_sessions", "DELETE", true)]
    [InlineData("auth_refresh_tokens", "SELECT", true)]
    [InlineData("auth_refresh_tokens", "INSERT", true)]
    [InlineData("auth_refresh_tokens", "UPDATE", true)]
    [InlineData("auth_refresh_tokens", "DELETE", true)]
    [InlineData("auth_events", "SELECT", true)]
    [InlineData("auth_events", "INSERT", true)]
    [InlineData("auth_events", "UPDATE", false)]
    [InlineData("auth_events", "DELETE", false)]
    public async Task Runtime_role_privileges_on_authentication_tables(string table, string privilege, bool expected)
    {
        await _fixture.RuntimeConnectionStringAsync(); // creates the role

        var allowed = await AuthTestSql.ScalarAsync(
            _fixture.AdminConnectionString,
            $"SELECT has_table_privilege('fynovio_app', 'identity.{table}', '{privilege}')");

        Assert.Equal(expected, (bool)allowed!);
    }

    [Fact]
    public async Task Auth_events_can_be_inserted_but_never_updated_or_deleted_by_the_runtime_role()
    {
        await using var runtime = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());

        await runtime.Database.ExecuteSqlRawAsync(
            "INSERT INTO identity.auth_events (occurred_at, event_type, outcome) VALUES (now(), 'test_event', 'success')");

        var update = await Assert.ThrowsAsync<PostgresException>(() =>
            runtime.Database.ExecuteSqlRawAsync("UPDATE identity.auth_events SET outcome = 'tampered'"));
        var delete = await Assert.ThrowsAsync<PostgresException>(() =>
            runtime.Database.ExecuteSqlRawAsync("DELETE FROM identity.auth_events"));

        Assert.Equal("42501", update.SqlState);
        Assert.Equal("42501", delete.SqlState);
    }

    /// <summary>The fixture inlines its own grants (it cannot execute the real script: it targets
    /// other schemas and a fixed database name), so guard against the shipped script drifting.</summary>
    [Fact]
    public void Shipped_runtime_role_script_keeps_auth_events_append_only()
    {
        var script = File.ReadAllText(AuthTestSql.RepoFile("scripts/create-runtime-role.sql"));

        Assert.Contains("REVOKE UPDATE, DELETE ON identity.auth_events FROM fynovio_app;", script);
        Assert.Contains("GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA identity TO fynovio_app;", script);
    }
}

/// <summary>Up → Down → Up of the authentication migration on a fresh database.</summary>
public sealed class AuthenticationMigrationRoundTripTests : IClassFixture<PostgresFixture>
{
    private const string PreviousMigration = "20260917084937_EnableAccessRowLevelSecurity";
    private static readonly string[] AuthTables =
        ["account_credentials", "auth_sessions", "auth_refresh_tokens", "auth_events"];

    private readonly PostgresFixture _fixture;

    public AuthenticationMigrationRoundTripTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<bool> TableExistsAsync(string table) =>
        await AuthTestSql.ScalarAsync(_fixture.AdminConnectionString, $"SELECT to_regclass('identity.{table}') IS NOT NULL") is true;

    private async Task<bool> PolicyExistsAsync() =>
        await AuthTestSql.ScalarAsync(
            _fixture.AdminConnectionString,
            "SELECT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname = 'identity' AND tablename = 'tenant_memberships' AND policyname = 'membership_self_view')") is true;

    [Fact]
    public async Task Down_removes_tables_and_policy_and_Up_restores_them()
    {
        foreach (var table in AuthTables)
            Assert.True(await TableExistsAsync(table), $"{table} should exist after the initial migrate");
        Assert.True(await PolicyExistsAsync());

        await using (var context = _fixture.CreateAdminContext())
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);

        foreach (var table in AuthTables)
            Assert.False(await TableExistsAsync(table), $"{table} should be gone after Down");
        Assert.False(await PolicyExistsAsync());
        // The pre-existing tenant_isolation policy must survive the Down.
        Assert.True(await AuthTestSql.ScalarAsync(
            _fixture.AdminConnectionString,
            "SELECT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname = 'identity' AND tablename = 'tenant_memberships' AND policyname = 'tenant_isolation')") is true);

        await using (var context = _fixture.CreateAdminContext())
            await context.Database.MigrateAsync();

        foreach (var table in AuthTables)
            Assert.True(await TableExistsAsync(table), $"{table} should exist again after Up");
        Assert.True(await PolicyExistsAsync());
    }
}
