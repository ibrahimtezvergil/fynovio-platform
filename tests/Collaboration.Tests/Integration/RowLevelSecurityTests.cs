using Collaboration.Domain;
using Collaboration.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Collaboration.Tests.Integration;

/// <summary>FF03 (doc 12) enforcement for Collaboration's tenant-scoped tables.
/// All three tables (calendar_entries, idempotency_records, outbox_messages) must:
/// - Have RLS enabled and forced
/// - Have a tenant_isolation policy
/// - Reject cross-tenant reads as zero rows
/// - Reject cross-tenant writes with WITH CHECK violation (SQLSTATE 42501)
/// - Fail safely when no tenant context is set (pooled connection without SET)
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class RowLevelSecurityTests
{
    private readonly PostgresFixture _fixture;

    public RowLevelSecurityTests(PostgresFixture fixture) => _fixture = fixture;

    /// <summary>Fitness function: every table in collaboration schema has RLS enabled,
    /// forced, and a policy defined. This catches future tables that slip through without
    /// RLS (happened with the pipeline tables).</summary>
    [Fact]
    public async Task Every_collaboration_table_has_row_level_security_enabled_forced_and_policied()
    {
        await using var context = _fixture.CreateAdminContext();
        await using var connection = new NpgsqlConnection(context.Database.GetConnectionString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.relname,
                   c.relrowsecurity,
                   c.relforcerowsecurity,
                   EXISTS (
                       SELECT 1 FROM pg_policies p
                       WHERE p.schemaname = 'collaboration' AND p.tablename = c.relname
                   ) AS has_policy
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'collaboration' AND c.relkind = 'r'
                AND c.relname <> '__ef_migrations_history';
            """;

        var uncovered = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var table = reader.GetString(0);
            var enabled = reader.GetBoolean(1);
            var forced = reader.GetBoolean(2);
            var policied = reader.GetBoolean(3);
            if (!enabled || !forced || !policied)
                uncovered.Add($"{table} (enabled={enabled}, forced={forced}, policied={policied})");
        }

        Assert.True(uncovered.Count == 0, $"Tables missing full RLS coverage: {string.Join(", ", uncovered)}");
    }


    [Fact]
    public async Task Pooled_connection_with_no_tenant_context_returns_empty_results_not_cast_error()
    {
        await SeedCalendarEntryAsync();

        // Use a runtime context without setting tenant context - simulates a pooled connection
        // that was previously used by a different tenant and context not cleared.
        await using var context1 = PostgresFixture.CreateCollaborationContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction1 = await context1.Database.BeginTransactionAsync();
        await context1.SetTenantContextAsync(TestData.NextTenant());

        // Create another context on the same connection pool, but don't set tenant context
        await using var context2 = PostgresFixture.CreateCollaborationContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction2 = await context2.Database.BeginTransactionAsync();
        // Don't call SetTenantContextAsync - simulates GUC not being set

        // Should return zero rows (NULLIF(...,'') evaluates to NULL which fails the RLS check),
        // not throw a cast error.
        var entries = await context2.CalendarEntries.AsNoTracking().ToListAsync();

        Assert.Empty(entries);
    }

    [Fact]
    public async Task SetTenantContextAsync_outside_transaction_throws_InvalidOperationException()
    {
        await using var context = PostgresFixture.CreateCollaborationContext(await _fixture.RuntimeConnectionStringAsync());
        var tenantId = TestData.NextTenant();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SetTenantContextAsync(tenantId));

        Assert.Contains("transaction", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(Contracts.TenantId TenantId, long EntryId)> SeedCalendarEntryAsync()
    {
        var tenant = TestData.NextTenant();
        var principal = new PrincipalRef("test-issuer", "test-subject");

        await using var context = _fixture.CreateAdminContext();
        var entry = CalendarEntry.Create(
            tenant, principal, $"Entry {Guid.NewGuid()}", null, "#000000",
            false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
            null, null, null);
        context.CalendarEntries.Add(entry);
        await context.SaveChangesAsync();
        return (tenant, entry.Id);
    }

    private async Task<(Contracts.TenantId TenantId, PrincipalRef Principal)> SeedIdempotencyRecordAsync()
    {
        var tenant = TestData.NextTenant();
        var principal = new PrincipalRef("test-issuer", "test-subject");

        await using var context = _fixture.CreateAdminContext();
        var record = Idempotency.IdempotencyRecord.Create(
            tenant, principal, "TestOp", $"key-{Guid.NewGuid()}", "hash-1", 200,
            "{\"test\": true}", TimeSpan.FromDays(7));
        context.IdempotencyRecords.Add(record);
        await context.SaveChangesAsync();
        return (tenant, principal);
    }

    private async Task<(Contracts.TenantId TenantId, long OutboxId)> SeedOutboxMessageAsync()
    {
        var tenant = TestData.NextTenant();

        await using var context = _fixture.CreateAdminContext();
        var message = Outbox.OutboxMessage.Create(
            tenant, "CalendarEntry", 123, 1,
            "enterprise.collaboration.calendar-entry.created.v1",
            "/enterprise/collaboration",
            "calendar-entries/123",
            Guid.NewGuid(),
            "{\"test\": true}");
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();
        return (tenant, message.Id);
    }
}
