using Access.Application.Authentication;
using Access.Domain.Authentication;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Access.Tests.Integration.Authentication;

public sealed class InvitationPersistenceConstraintsTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Pending_delivery_must_keep_a_protected_token()
    {
        var tenant = AuthTestSetup.NewTenant();
        var invitationId = Guid.NewGuid();
        await using var context = fixture.CreateAdminContext();
        context.InvitationDeliveries.Add(InvitationDelivery.Create(tenant, invitationId, "protected", DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE access.invitation_deliveries SET protected_token = '' WHERE tenant_id = {tenant.Value} AND invitation_id = {invitationId}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_invitation_deliveries_state", exception.ConstraintName);
    }

    [Fact]
    public async Task Password_reset_token_cannot_carry_an_invited_role()
    {
        var tenant = AuthTestSetup.NewTenant();
        var (accountId, _) = await AuthTestSetup.SeedTenantAdminAsync(fixture, tenant);
        var (_, hash) = AccountTokenService.NewSecret();
        var token = AccountToken.CreatePasswordReset(accountId, hash, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(30));
        await using var context = fixture.CreateAdminContext();
        context.AccountTokens.Add(token);
        await context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE identity.account_tokens SET invited_role_key = 'tenant_administrator' WHERE id = {token.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_account_tokens_invited_role_shape", exception.ConstraintName);
    }
}
