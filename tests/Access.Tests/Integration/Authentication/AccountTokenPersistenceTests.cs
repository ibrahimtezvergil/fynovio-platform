using Access.Domain.Authentication;
using Access.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Access.Tests.Integration.Authentication;

/// <summary>`identity.account_tokens`: DB-enforced shape per purpose, privileges of the runtime
/// role (consuming a token needs UPDATE), and the migration round trip.</summary>
public sealed class AccountTokenPersistenceTests : IClassFixture<PostgresFixture>
{
    private const string PreviousMigration = "20260919235903_AddAuthenticationFoundation";

    private readonly PostgresFixture _fixture;

    public AccountTokenPersistenceTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<long> InsertAccountAsync()
    {
        await using var admin = _fixture.CreateAdminContext();
        var account = Account.Create($"t-{Guid.NewGuid():N}@example.com", "Token Test");
        admin.Accounts.Add(account);
        await admin.SaveChangesAsync();
        return account.Id;
    }

    private static Task<int> InsertRawAsync(Access.Persistence.AccessDbContext context, string purpose, long? accountId, long? tenantId, string? email) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO identity.account_tokens (id, purpose, token_hash, account_id, tenant_id, email_normalized, expires_at, created_at)
            VALUES ({Guid.NewGuid()}, {purpose}, {Guid.NewGuid().ToString("N")}, {accountId}, {tenantId}, {email}, now() + interval '1 day', now())
            """);

    [Fact]
    public async Task Database_rejects_an_unknown_purpose()
    {
        var accountId = await InsertAccountAsync();
        await using var admin = _fixture.CreateAdminContext();

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertRawAsync(admin, "magic_link", accountId, null, null));

        Assert.Equal("23514", failure.SqlState);
        Assert.Equal("ck_account_tokens_purpose", failure.ConstraintName);
    }

    [Fact]
    public async Task Database_requires_tenant_and_email_on_invites()
    {
        await using var admin = _fixture.CreateAdminContext();

        var withoutTenant = await Assert.ThrowsAsync<PostgresException>(() => InsertRawAsync(admin, "invite", null, null, "a@example.com"));
        var withoutEmail = await Assert.ThrowsAsync<PostgresException>(() => InsertRawAsync(admin, "invite", null, 1, null));

        Assert.Equal("ck_account_tokens_invite_shape", withoutTenant.ConstraintName);
        Assert.Equal("ck_account_tokens_invite_shape", withoutEmail.ConstraintName);
        Assert.Equal(1, await InsertRawAsync(admin, "invite", null, 1, "a@example.com")); // a well-formed invite is fine
    }

    [Theory]
    [InlineData("password_reset")]
    [InlineData("password_setup")]
    public async Task Database_requires_an_account_on_reset_and_setup_tokens(string purpose)
    {
        await using var admin = _fixture.CreateAdminContext();

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertRawAsync(admin, purpose, null, null, null));

        Assert.Equal("ck_account_tokens_account_purposes_have_account", failure.ConstraintName);
        Assert.Equal(1, await InsertRawAsync(admin, purpose, await InsertAccountAsync(), null, null));
    }

    [Fact]
    public async Task Token_hash_is_unique()
    {
        var accountId = await InsertAccountAsync();
        await using var admin = _fixture.CreateAdminContext();
        admin.AccountTokens.Add(AccountToken.CreatePasswordReset(accountId, "same-hash", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5)));
        await admin.SaveChangesAsync();

        await using var second = _fixture.CreateAdminContext();
        second.AccountTokens.Add(AccountToken.CreatePasswordReset(accountId, "same-hash", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5)));
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());

        Assert.Equal("23505", Assert.IsType<PostgresException>(failure.InnerException).SqlState);
    }

    [Theory]
    [InlineData("SELECT", true)]
    [InlineData("INSERT", true)]
    [InlineData("UPDATE", true)] // consuming/revoking a token is an UPDATE
    public async Task Runtime_role_can_read_create_and_update_tokens(string privilege, bool expected)
    {
        await _fixture.RuntimeConnectionStringAsync();

        await using var connection = new NpgsqlConnection(_fixture.AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"SELECT has_table_privilege('fynovio_app', 'identity.account_tokens', '{privilege}')", connection);

        Assert.Equal(expected, (bool)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Only_the_hash_is_persisted_never_the_secret()
    {
        var accountId = await InsertAccountAsync();
        var (secret, hash) = Access.Application.Authentication.TokenSecrets.GenerateAndHash();
        await using (var admin = _fixture.CreateAdminContext())
        {
            admin.AccountTokens.Add(AccountToken.CreatePasswordReset(accountId, hash, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5)));
            await admin.SaveChangesAsync();
        }

        await using var connection = new NpgsqlConnection(_fixture.AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT row_to_json(t)::text FROM identity.account_tokens t WHERE token_hash = @h", connection);
        command.Parameters.AddWithValue("h", hash);
        var row = (string)(await command.ExecuteScalarAsync())!;

        Assert.DoesNotContain(secret, row);
        Assert.NotEqual(secret, hash);
    }

    [Fact]
    public async Task Down_drops_the_table_and_Up_restores_it()
    {
        Assert.True(await TableExistsAsync());

        await using (var context = _fixture.CreateAdminContext())
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        Assert.False(await TableExistsAsync());

        await using (var context = _fixture.CreateAdminContext())
            await context.Database.MigrateAsync();
        Assert.True(await TableExistsAsync());
    }

    private async Task<bool> TableExistsAsync()
    {
        await using var connection = new NpgsqlConnection(_fixture.AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT to_regclass('identity.account_tokens') IS NOT NULL", connection);
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
