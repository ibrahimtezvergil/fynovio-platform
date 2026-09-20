using Access.Application.Authentication;
using Access.Domain.Authentication;
using Access.Domain.Identity;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Access.Tests.Integration.Authentication;

/// <summary>Records what the Access layer hands to the mail transport.</summary>
internal sealed class FakeEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _messages = [];

    public IReadOnlyList<EmailMessage> Messages
    {
        get { lock (_messages) return _messages.ToList(); }
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        lock (_messages) _messages.Add(message);
        return Task.CompletedTask;
    }

    /// <summary>The raw single-use token of the most recent message to <paramref name="to"/>.</summary>
    public string TokenFor(string to) =>
        Messages.Last(m => string.Equals(m.To, to, StringComparison.OrdinalIgnoreCase)).Values["token"];
}

/// <summary>Shared arrange helpers for the invitation / password-lifecycle tests. Tests in a class
/// share one container, so every test uses its own e-mail addresses, tenants and clock.</summary>
internal static class AuthTestSetup
{
    public const string Issuer = "https://platform.example.com";
    public const string Password = "Correct-Horse-Battery-9";

    public static SessionOptions Session => new() { PlatformIssuer = Issuer };

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    public static TenantId NewTenant() => new(AuthTestSql.NextTenantBlock());

    public static async Task<AccessDbContext> RuntimeContextAsync(PostgresFixture fixture) =>
        PostgresFixture.CreateContext(await fixture.RuntimeConnectionStringAsync());

    public static AccountTokenService Tokens(AccessDbContext context, TokenOptions? options = null) =>
        new(context, options ?? new TokenOptions());

    /// <summary>An account with a credential (and optionally an Active membership) through the real handler.</summary>
    public static async Task<(long AccountId, PrincipalRef Principal)> SeedAccountAsync(
        PostgresFixture fixture, string email, string password = Password, TenantId? tenant = null, TimeProvider? time = null)
    {
        await using var context = await RuntimeContextAsync(fixture);
        var result = await new ProvisionPasswordAccountHandler(context, new PasswordService(), new PasswordPolicy(new PasswordPolicyOptions()), time)
            .HandleAsync(new ProvisionPasswordAccountCommand(email, "Seeded User", password, Issuer, tenant));
        return (result.AccountId, result.Principal);
    }

    /// <summary>Inserts a token straight into the table (as the superuser) and returns the raw `<id>.<secret>` value.</summary>
    public static async Task<string> InsertTokenAsync(PostgresFixture fixture, Func<string, AccountToken> create)
    {
        var (secret, hash) = AccountTokenService.NewSecret();
        var token = create(hash);
        await using var admin = fixture.CreateAdminContext();
        admin.AccountTokens.Add(token);
        await admin.SaveChangesAsync();
        return AccountTokenService.Compose(token.Id, secret);
    }

    public static async Task<AccountToken> LoadTokenAsync(PostgresFixture fixture, string raw)
    {
        Assert.True(AccountTokenService.TryParse(raw, out var id, out _));
        await using var admin = fixture.CreateAdminContext();
        return await admin.AccountTokens.AsNoTracking().SingleAsync(t => t.Id == id);
    }

    public static async Task<long> ScalarLongAsync(PostgresFixture fixture, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(fixture.AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }
}
