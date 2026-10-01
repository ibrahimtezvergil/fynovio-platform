using Npgsql;

namespace Messaging.Tests;

/// <summary>The one new cross-tenant surface (E-1 (a)) is the outbox pointer columns, and nothing else.</summary>
[Collection(MessagingCollection.Name)]
public sealed class RelayRoleTests(MessagingFixture fixture)
{
    public static TheoryData<string> Schemas => new(OutboxSources.All);

    [Theory]
    [MemberData(nameof(Schemas))]
    public async Task The_relay_sees_the_pointers_of_every_tenant_but_never_a_payload(string schema)
    {
        var (tenantA, tenantB) = (TestTenants.Next(), TestTenants.Next());
        await fixture.InsertOutboxAsync(schema, tenantA, "test.seen.v1");
        await fixture.InsertOutboxAsync(schema, tenantB, "test.seen.v1");

        await using var relay = new NpgsqlConnection(fixture.RelayConnectionString);
        await relay.OpenAsync();

        await using (var count = new NpgsqlCommand($"SELECT count(*) FROM {schema}.outbox_messages WHERE tenant_id IN ({tenantA}, {tenantB})", relay))
            Assert.Equal(2L, await count.ExecuteScalarAsync());

        foreach (var sql in new[]
        {
            $"SELECT payload FROM {schema}.outbox_messages",
            $"SELECT * FROM {schema}.outbox_messages",
            $"SELECT source, subject, correlation_id, causation_id FROM {schema}.outbox_messages",
            $"DELETE FROM {schema}.outbox_messages",
            $"UPDATE {schema}.outbox_messages SET event_type = 'x'",
            $"INSERT INTO {schema}.outbox_messages (tenant_id) VALUES (1)"
        })
        {
            await using var command = new NpgsqlCommand(sql, relay);
            var denied = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }
    }

    [Theory]
    [InlineData("crm.opportunities")]
    [InlineData("crm.evidence_records")]
    [InlineData("crm.idempotency_records")]
    [InlineData("masterdata.parties")]
    [InlineData("collaboration.calendar_entries")]
    [InlineData("tenant_lifecycle.tenant_profiles")]
    [InlineData("access.tenant_access_state")]
    [InlineData("identity.tenant_memberships")]
    public async Task The_relay_cannot_read_a_business_table(string table)
    {
        await using var relay = new NpgsqlConnection(fixture.RelayConnectionString);
        await relay.OpenAsync();
        await using var command = new NpgsqlCommand($"SELECT 1 FROM {table} LIMIT 1", relay);

        var denied = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteScalarAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
    }

    [Theory]
    [MemberData(nameof(Schemas))]
    public async Task The_runtime_role_still_sees_only_its_own_tenant(string schema)
    {
        var (tenantA, tenantB) = (TestTenants.Next(), TestTenants.Next());
        await fixture.InsertOutboxAsync(schema, tenantA, "test.isolated.v1");
        await fixture.InsertOutboxAsync(schema, tenantB, "test.isolated.v1");

        await using var runtime = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await runtime.OpenAsync();

        // No tenant context: nothing, exactly as before the relay policies existed.
        await using (var none = new NpgsqlCommand($"SELECT count(*) FROM {schema}.outbox_messages", runtime))
            Assert.Equal(0L, await none.ExecuteScalarAsync());

        await using var transaction = await runtime.BeginTransactionAsync();
        await using (var set = new NpgsqlCommand($"SELECT set_config('app.tenant_id', '{tenantA}', true)", runtime, transaction))
            await set.ExecuteNonQueryAsync();
        await using var own = new NpgsqlCommand($"SELECT count(*) FROM {schema}.outbox_messages WHERE tenant_id IN ({tenantA}, {tenantB})", runtime, transaction);
        Assert.Equal(1L, await own.ExecuteScalarAsync());
    }

    [Fact]
    public async Task The_runtime_role_sees_only_its_own_tenants_deliveries_and_cannot_create_one()
    {
        var (tenantA, tenantB) = (TestTenants.Next(), TestTenants.Next());
        foreach (var tenant in new[] { tenantA, tenantB })
        {
            await fixture.AdminExecuteAsync(
                """
                INSERT INTO messaging.event_deliveries (tenant_id, consumer, source_schema, source_id, event_id, event_type, aggregate_type, aggregate_id, aggregate_version, status, attempts, next_attempt_at)
                VALUES (@tenant, 'isolation-probe', 'crm', 1, gen_random_uuid(), 'test.v1', 'Thing', 1, 1, 'dead', 10, now())
                """,
                ("tenant", tenant));
        }

        await using var runtime = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await runtime.OpenAsync();
        await using (var none = new NpgsqlCommand("SELECT count(*) FROM messaging.event_deliveries", runtime))
            Assert.Equal(0L, await none.ExecuteScalarAsync());

        await using var transaction = await runtime.BeginTransactionAsync();
        await using (var set = new NpgsqlCommand($"SELECT set_config('app.tenant_id', '{tenantA}', true)", runtime, transaction))
            await set.ExecuteNonQueryAsync();
        await using (var own = new NpgsqlCommand($"SELECT count(*) FROM messaging.event_deliveries WHERE tenant_id IN ({tenantA}, {tenantB})", runtime, transaction))
            Assert.Equal(1L, await own.ExecuteScalarAsync());

        await using var insert = new NpgsqlCommand(
            $"INSERT INTO messaging.event_deliveries (tenant_id, consumer, source_schema, source_id, event_id, event_type, aggregate_type, aggregate_id, aggregate_version, status, attempts, next_attempt_at) VALUES ({tenantA}, 'x', 'crm', 1, gen_random_uuid(), 't', 'T', 1, 1, 'pending', 0, now())",
            runtime, transaction);
        var denied = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
    }
}
