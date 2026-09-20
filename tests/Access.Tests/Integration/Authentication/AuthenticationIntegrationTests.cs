using Access.Application.Authentication;
using Access.Domain.Authentication;
using Access.Domain.Identity;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Access.Tests.Integration.Authentication;

public sealed class AuthenticationIntegrationTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture = new();
    private AccessDbContext _context = null!;

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
        var runtimeConnString = await _fixture.RuntimeConnectionStringAsync();
        _context = PostgresFixture.CreateContext(runtimeConnString);
    }

    public async Task DisposeAsync()
    {
        _context.Dispose();
        await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task ProvisionPasswordAccountHandler_IdempotentByNormalizedEmail()
    {
        var handler = new ProvisionPasswordAccountHandler(
            _context,
            new PasswordService(),
            new PasswordPolicy(new PasswordPolicyOptions()),
            _fixture.TimeProvider);

        var cmd = new ProvisionPasswordAccountCommand(
            "test@example.com",
            "Test User",
            "ValidPassword123",
            "https://platform.example.com");

        var result1 = await handler.HandleAsync(cmd);
        var result2 = await handler.HandleAsync(cmd);

        Assert.Equal(result1.AccountId, result2.AccountId);
        Assert.Equal(result1.Principal, result2.Principal);
    }

    [Fact]
    public async Task AuthenticateHandler_UnknownUser_InvalidCredentials()
    {
        var handler = new AuthenticateHandler(
            _context,
            new PasswordService(),
            new LockoutOptions(),
            new SessionOptions { PlatformIssuer = "https://platform.example.com" },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        var result = await handler.HandleAsync(
            new AuthenticateCommand("unknown@example.com", "password123"));

        Assert.Equal(AuthenticationStatus.InvalidCredentials, result.Status);
    }

    [Fact]
    public async Task AuthenticateHandler_BadPassword_InvalidCredentials()
    {
        // Create account and credential
        var accountId = await ProvisionAccount("test@example.com", "ValidPassword123");

        var handler = new AuthenticateHandler(
            _context,
            new PasswordService(),
            new LockoutOptions(),
            new SessionOptions { PlatformIssuer = "https://platform.example.com" },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        var result = await handler.HandleAsync(
            new AuthenticateCommand("test@example.com", "WrongPassword"));

        Assert.Equal(AuthenticationStatus.InvalidCredentials, result.Status);
    }

    [Fact]
    public async Task AuthenticateHandler_LockedAccount_InvalidCredentials()
    {
        var accountId = await ProvisionAccount("test@example.com", "ValidPassword123");
        var credential = await _context.AccountCredentials.FirstAsync(c => c.AccountId == accountId);

        // Lock the account (5 failed attempts)
        for (int i = 0; i < 5; i++)
            credential.RecordFailedAttempt(5, 15, _fixture.TimeProvider.GetUtcNow());
        await _context.SaveChangesAsync();

        var handler = new AuthenticateHandler(
            _context,
            new PasswordService(),
            new LockoutOptions(),
            new SessionOptions { PlatformIssuer = "https://platform.example.com" },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        var result = await handler.HandleAsync(
            new AuthenticateCommand("test@example.com", "ValidPassword123"));

        Assert.Equal(AuthenticationStatus.InvalidCredentials, result.Status);
    }

    [Fact]
    public async Task AuthenticateHandler_SuccessfulLogin_CreatesSession()
    {
        var accountId = await ProvisionAccount("test@example.com", "ValidPassword123");

        var handler = new AuthenticateHandler(
            _context,
            new PasswordService(),
            new LockoutOptions(),
            new SessionOptions { PlatformIssuer = "https://platform.example.com", RefreshAbsoluteDays = 30 },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        var result = await handler.HandleAsync(
            new AuthenticateCommand("test@example.com", "ValidPassword123"));

        Assert.Equal(AuthenticationStatus.NoMembership, result.Status);
        Assert.NotNull(result.SessionId);
        Assert.NotEmpty(result.RefreshCookie!);
    }

    [Fact]
    public async Task AuthenticateHandler_SingleMembership_AutoSelects()
    {
        var accountId = await ProvisionAccount("test@example.com", "ValidPassword123");
        var tenantId = new TenantId(1);

        // Create active membership
        await using var tx = await _context.Database.BeginTransactionAsync();
        await _context.SetTenantContextAsync(tenantId);
        var membership = TenantMembership.Invite(tenantId, accountId);
        membership.Activate();
        _context.TenantMemberships.Add(membership);
        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        var handler = new AuthenticateHandler(
            _context,
            new PasswordService(),
            new LockoutOptions(),
            new SessionOptions { PlatformIssuer = "https://platform.example.com" },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        var result = await handler.HandleAsync(
            new AuthenticateCommand("test@example.com", "ValidPassword123"));

        Assert.Equal(AuthenticationStatus.Authenticated, result.Status);
        Assert.Equal(tenantId.Value, result.SelectedTenantId);
    }

    [Fact]
    public async Task AuthenticateHandler_MultipleMemberships_RequireSelection()
    {
        var accountId = await ProvisionAccount("test@example.com", "ValidPassword123");

        // Create active memberships in two tenants
        for (int i = 1; i <= 2; i++)
        {
            var tenantId = new TenantId(i);
            await using var tx = await _context.Database.BeginTransactionAsync();
            await _context.SetTenantContextAsync(tenantId);
            var membership = TenantMembership.Invite(tenantId, accountId);
            membership.Activate();
            _context.TenantMemberships.Add(membership);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }

        var handler = new AuthenticateHandler(
            _context,
            new PasswordService(),
            new LockoutOptions(),
            new SessionOptions { PlatformIssuer = "https://platform.example.com" },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        var result = await handler.HandleAsync(
            new AuthenticateCommand("test@example.com", "ValidPassword123"));

        Assert.Equal(AuthenticationStatus.TenantSelectionRequired, result.Status);
        Assert.Null(result.SelectedTenantId);
        Assert.Equal(2, result.MembershipTenantIds!.Count);
    }

    [Fact]
    public async Task RefreshSessionHandler_ValidToken_RotatesToken()
    {
        var (session, refreshCookie) = await CreateSession();

        var handler = new RefreshSessionHandler(
            _context,
            new SessionOptions { RefreshAbsoluteDays = 30 },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        var result = await handler.HandleAsync(
            new RefreshSessionCommand(refreshCookie));

        Assert.Equal(RefreshResult.Success, result.Status);
        Assert.NotEmpty(result.RefreshCookie!);
        Assert.NotEqual(refreshCookie, result.RefreshCookie);
    }

    [Fact]
    public async Task RefreshSessionHandler_ReuseOutsideGrace_RevokesSession()
    {
        var (session, refreshCookie) = await CreateSession();

        var handler = new RefreshSessionHandler(
            _context,
            new SessionOptions { RefreshAbsoluteDays = 30, RefreshGraceSeconds = 5 },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        // First refresh (OK)
        var result1 = await handler.HandleAsync(new RefreshSessionCommand(refreshCookie));
        Assert.Equal(RefreshResult.Success, result1.Status);

        // Advance time past grace window and retry with original token
        _fixture.TimeProvider.Advance(TimeSpan.FromSeconds(10));

        var result2 = await handler.HandleAsync(new RefreshSessionCommand(refreshCookie));
        Assert.Equal(RefreshResult.SessionInvalid, result2.Status);

        // Session should be revoked
        var revokedSession = await _context.AuthSessions.FirstAsync(s => s.Id == session.Id);
        Assert.True(revokedSession.IsRevoked);
        Assert.Equal("reuse_detected", revokedSession.RevokedReason);
    }

    [Fact]
    public async Task RefreshSessionHandler_WithinGrace_ReturnsConflict()
    {
        var (session, refreshCookie) = await CreateSession();

        var handler = new RefreshSessionHandler(
            _context,
            new SessionOptions { RefreshAbsoluteDays = 30, RefreshGraceSeconds = 5 },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        // First refresh (OK)
        var result1 = await handler.HandleAsync(new RefreshSessionCommand(refreshCookie));
        Assert.Equal(RefreshResult.Success, result1.Status);

        // Retry within grace window with original token
        _fixture.TimeProvider.Advance(TimeSpan.FromSeconds(2));

        var result2 = await handler.HandleAsync(new RefreshSessionCommand(refreshCookie));
        Assert.Equal(RefreshResult.RefreshConflict, result2.Status);

        // Session should NOT be revoked
        var session2 = await _context.AuthSessions.FirstAsync(s => s.Id == session.Id);
        Assert.False(session2.IsRevoked);
    }

    [Fact]
    public async Task LogoutHandler_IdempotentRevocation()
    {
        var (session, refreshCookie) = await CreateSession();

        var handler = new LogoutHandler(
            _context,
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        // First logout
        await handler.HandleAsync(refreshCookie);
        var revokedSession = await _context.AuthSessions.FirstAsync(s => s.Id == session.Id);
        Assert.True(revokedSession.IsRevoked);
        Assert.Equal("logout", revokedSession.RevokedReason);

        // Second logout (idempotent, no error)
        await handler.HandleAsync(refreshCookie); // Should not throw
    }

    [Fact]
    public async Task SelectTenantHandler_ValidMembership_SelectsTenant()
    {
        var accountId = await ProvisionAccount("test@example.com", "ValidPassword123");
        var (session, refreshCookie) = await CreateSession(accountId);
        var tenantId = new TenantId(1);

        // Create active membership
        await using var tx = await _context.Database.BeginTransactionAsync();
        await _context.SetTenantContextAsync(tenantId);
        var membership = TenantMembership.Invite(tenantId, accountId);
        membership.Activate();
        _context.TenantMemberships.Add(membership);
        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        var handler = new SelectTenantHandler(
            _context,
            new SessionOptions { PlatformIssuer = "https://platform.example.com" },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        var result = await handler.HandleAsync(
            new SelectTenantCommand(refreshCookie, tenantId));

        Assert.Equal(TenantSelectionStatus.Success, result.Status);
        Assert.Equal(tenantId.Value, result.SelectedTenantId);
    }

    [Fact]
    public async Task SelectTenantHandler_NonMember_TenantNotPermitted()
    {
        var (session, refreshCookie) = await CreateSession();
        var tenantId = new TenantId(1);

        var handler = new SelectTenantHandler(
            _context,
            new SessionOptions { PlatformIssuer = "https://platform.example.com" },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        var result = await handler.HandleAsync(
            new SelectTenantCommand(refreshCookie, tenantId));

        Assert.Equal(TenantSelectionStatus.TenantNotPermitted, result.Status);
    }

    [Fact]
    public async Task AuthEventWriter_NeverPersistsSecrets()
    {
        var writer = new AuthEventWriter(_context);

        var detail = new Dictionary<string, object?>
        {
            { "attemptCount", 5 },
            { "password", "SECRET_PASSWORD" }, // Should be filtered
            { "token", "SECRET_TOKEN" } // Should be filtered
        };

        await writer.WriteAsync(
            "test_event",
            "outcome",
            _fixture.TimeProvider.GetUtcNow(),
            detail: detail);

        var evt = await _context.AuthEvents.FirstAsync();
        Assert.DoesNotContain("password", evt.Detail ?? "");
        Assert.DoesNotContain("token", evt.Detail ?? "");
        Assert.Contains("attemptCount", evt.Detail ?? "");
    }

    // Helper methods
    private async Task<long> ProvisionAccount(string email, string password)
    {
        var handler = new ProvisionPasswordAccountHandler(
            _context,
            new PasswordService(),
            new PasswordPolicy(new PasswordPolicyOptions()),
            _fixture.TimeProvider);

        var result = await handler.HandleAsync(
            new ProvisionPasswordAccountCommand(email, "Test User", password, "https://platform.example.com"));

        return result.AccountId;
    }

    private async Task<(AuthSession session, string cookieValue)> CreateSession(long? accountId = null)
    {
        // Use account 1 if not specified
        accountId ??= await ProvisionAccount("session@example.com", "ValidPassword123");

        var now = _fixture.TimeProvider.GetUtcNow();
        var session = AuthSession.Create(accountId.Value, now, now.AddDays(30));
        var (tokenSecret, tokenHash) = TokenSecrets.GenerateAndHash();
        var token = RefreshToken.Create(session.Id, tokenHash, now, now.AddDays(7));

        _context.AuthSessions.Add(session);
        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync();

        var cookieValue = $"{token.Id}.{tokenSecret}";
        return (session, cookieValue);
    }
}
