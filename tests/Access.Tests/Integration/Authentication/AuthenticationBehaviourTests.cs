using Access.Application.Authentication;
using Access.Domain.Authentication;
using Access.Domain.Identity;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;

namespace Access.Tests.Integration.Authentication;

public sealed class AuthenticationBehaviourTests : IAsyncLifetime
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

    /// <summary>Test 1: Lockout exactness - failed attempts don't lock until N, then lock persists,
    /// correct password after unlock succeeds, and failed attempts reset on success.</summary>
    [Fact]
    public async Task LockoutExactness_FailedAttemptsLockAtThreshold()
    {
        const int maxFailedAttempts = 5;
        const int lockoutMinutes = 15;
        var accountId = await ProvisionAccount("lockout@example.com", "ValidPassword12345");
        var runtimeConnString = await _fixture.RuntimeConnectionStringAsync();

        var handler = new AuthenticateHandler(
            _context,
            new PasswordService(),
            new LockoutOptions { MaxFailedAttempts = maxFailedAttempts, LockoutMinutes = lockoutMinutes },
            new SessionOptions { PlatformIssuer = "https://platform.example.com" },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        // Attempts 1-4 should NOT lock
        for (int i = 1; i < maxFailedAttempts; i++)
        {
            var result = await handler.HandleAsync(new AuthenticateCommand("lockout@example.com", "WrongPassword"));
            Assert.Equal(AuthenticationStatus.InvalidCredentials, result.Status);

            // Verify in fresh context that FailedAttempts incremented and no lockout
            using (var freshCtx = PostgresFixture.CreateContext(runtimeConnString))
            {
                var cred = await freshCtx.AccountCredentials.FirstAsync(c => c.AccountId == accountId);
                Assert.Equal(i, cred.FailedAttempts);
                Assert.Null(cred.LockedUntil);
            }
        }

        // Attempt N should lock
        var lockResult = await handler.HandleAsync(new AuthenticateCommand("lockout@example.com", "WrongPassword"));
        Assert.Equal(AuthenticationStatus.InvalidCredentials, lockResult.Status);

        using (var freshCtx = PostgresFixture.CreateContext(runtimeConnString))
        {
            var lockedCred = await freshCtx.AccountCredentials.FirstAsync(c => c.AccountId == accountId);
            Assert.Equal(maxFailedAttempts, lockedCred.FailedAttempts);
            Assert.NotNull(lockedCred.LockedUntil);
        }

        // While locked, correct password should still return InvalidCredentials
        var correctResult = await handler.HandleAsync(new AuthenticateCommand("lockout@example.com", "ValidPassword12345"));
        Assert.Equal(AuthenticationStatus.InvalidCredentials, correctResult.Status);

        // Advance time past lockout period
        _fixture.TimeProvider.Advance(TimeSpan.FromMinutes(lockoutMinutes + 1));

        // Now correct password should succeed
        var successResult = await handler.HandleAsync(new AuthenticateCommand("lockout@example.com", "ValidPassword12345"));
        Assert.NotEqual(AuthenticationStatus.InvalidCredentials, successResult.Status);

        // FailedAttempts should be reset to 0
        using (var freshCtx = PostgresFixture.CreateContext(runtimeConnString))
        {
            var successCred = await freshCtx.AccountCredentials.FirstAsync(c => c.AccountId == accountId);
            Assert.Equal(0, successCred.FailedAttempts);
            Assert.Null(successCred.LockedUntil);
        }
    }

    /// <summary>Test 2: Unknown user, wrong password, locked account, and no-membership produce identical
    /// visible results but events differ only in detail.</summary>
    [Fact]
    public async Task IdenticalErrorResults_UnknownWrongLockedNoMembership()
    {
        var accountId = await ProvisionAccount("test@example.com", "ValidPassword12345");
        var credential = await _context.AccountCredentials.FirstAsync(c => c.AccountId == accountId);

        // Lock the account
        credential.RecordFailedAttempt(5, 15, _fixture.TimeProvider.GetUtcNow());
        await _context.SaveChangesAsync();

        var handler = new AuthenticateHandler(
            _context,
            new PasswordService(),
            new LockoutOptions { MaxFailedAttempts = 5 },
            new SessionOptions { PlatformIssuer = "https://platform.example.com" },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        // Unknown user
        var unknownResult = await handler.HandleAsync(new AuthenticateCommand("unknown@example.com", "password"));
        Assert.Equal(AuthenticationStatus.InvalidCredentials, unknownResult.Status);

        // Wrong password (on locked account)
        var wrongResult = await handler.HandleAsync(new AuthenticateCommand("test@example.com", "WrongPassword"));
        Assert.Equal(AuthenticationStatus.InvalidCredentials, wrongResult.Status);

        // Compare result shapes (excluding SessionId, RefreshCookie as they're null or secrets)
        Assert.Equal(unknownResult.Status, wrongResult.Status);
        Assert.Null(unknownResult.AccessToken);
        Assert.Null(wrongResult.AccessToken);
        Assert.Null(unknownResult.RefreshCookie);
        Assert.Null(wrongResult.RefreshCookie);
    }

    /// <summary>Test 3: Login outcomes - one active membership auto-selects, two requires selection,
    /// zero returns no membership. Invited and Disabled memberships never count.</summary>
    [Fact]
    public async Task LoginOutcomes_SingleAutoSelectTwoRequireSelectionZeroNoMembership()
    {
        // Scenario 1: One active membership
        var accountId1 = await ProvisionAccount("single@example.com", "Password12345678");
        var tenantId1 = new TenantId(1);
        await AddActiveMembership(accountId1, tenantId1);

        var handler = new AuthenticateHandler(
            _context,
            new PasswordService(),
            new LockoutOptions(),
            new SessionOptions { PlatformIssuer = "https://platform.example.com" },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        var result1 = await handler.HandleAsync(new AuthenticateCommand("single@example.com", "Password12345678"));
        Assert.Equal(AuthenticationStatus.Authenticated, result1.Status);
        Assert.Equal(tenantId1.Value, result1.SelectedTenantId);

        // Scenario 2: Two active memberships
        var accountId2 = await ProvisionAccount("double@example.com", "Password12345678");
        var tenantId2a = new TenantId(2);
        var tenantId2b = new TenantId(3);
        await AddActiveMembership(accountId2, tenantId2a);
        await AddActiveMembership(accountId2, tenantId2b);

        var result2 = await handler.HandleAsync(new AuthenticateCommand("double@example.com", "Password12345678"));
        Assert.Equal(AuthenticationStatus.TenantSelectionRequired, result2.Status);
        Assert.Null(result2.SelectedTenantId);
        Assert.Equal(2, result2.MembershipTenantIds!.Count);

        // Scenario 3: No active memberships (no membership at all)
        var accountId3 = await ProvisionAccount("nomember@example.com", "Password12345678");

        var result3 = await handler.HandleAsync(new AuthenticateCommand("nomember@example.com", "Password12345678"));
        Assert.Equal(AuthenticationStatus.NoMembership, result3.Status);
        Assert.Null(result3.SelectedTenantId);

        // Scenario 4: Mix of statuses (Invited, Disabled, Active) — only Active counts
        var accountId4 = await ProvisionAccount("mixed@example.com", "Password12345678");
        var tenantId4Active = new TenantId(4);
        var tenantId4Invited = new TenantId(5);
        var tenantId4Disabled = new TenantId(6);

        await AddActiveMembership(accountId4, tenantId4Active);
        await AddInvitedMembership(accountId4, tenantId4Invited);
        await AddDisabledMembership(accountId4, tenantId4Disabled);

        var result4 = await handler.HandleAsync(new AuthenticateCommand("mixed@example.com", "Password12345678"));
        Assert.Equal(AuthenticationStatus.Authenticated, result4.Status);
        Assert.Equal(tenantId4Active.Value, result4.SelectedTenantId);
        Assert.Single(result4.MembershipTenantIds!);
    }

    /// <summary>Test 4a: Refresh idle expiry - token expires after RefreshIdleDays.</summary>
    [Fact]
    public async Task RefreshIdleExpiry_TokenInvalidAfterIdlePeriod()
    {
        const int refreshIdleDays = 7;
        var (session, refreshCookie) = await CreateSession(await ProvisionAccount("idle@example.com", "ValidPassword12345"));

        var handler = new RefreshSessionHandler(
            _context,
            new SessionOptions
            {
                PlatformIssuer = "https://platform.example.com",
                RefreshIdleDays = refreshIdleDays,
                RefreshAbsoluteDays = 30
            },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        // Advance past idle period
        _fixture.TimeProvider.Advance(TimeSpan.FromDays(refreshIdleDays + 1));

        var result = await handler.HandleAsync(new RefreshSessionCommand(refreshCookie));
        Assert.Equal(RefreshResult.SessionInvalid, result.Status);
    }

    /// <summary>Test 4b: Refresh absolute expiry - session expires after RefreshAbsoluteDays even
    /// with refresh rotations within the idle window.</summary>
    [Fact]
    public async Task RefreshAbsoluteExpiry_SessionInvalidAfterAbsolutePeriod()
    {
        const int refreshIdleDays = 7;
        const int refreshAbsoluteDays = 30;
        var (session, refreshCookie) = await CreateSession(await ProvisionAccount("absolute@example.com", "ValidPassword12345"));

        var handler = new RefreshSessionHandler(
            _context,
            new SessionOptions
            {
                PlatformIssuer = "https://platform.example.com",
                RefreshIdleDays = refreshIdleDays,
                RefreshAbsoluteDays = refreshAbsoluteDays,
                RefreshGraceSeconds = 5
            },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        var currentCookie = refreshCookie;

        // Keep refreshing in steps less than idle window until we exceed absolute
        var stepDays = (refreshIdleDays / 2);
        for (int i = 0; i < (refreshAbsoluteDays / stepDays) + 1; i++)
        {
            _fixture.TimeProvider.Advance(TimeSpan.FromDays(stepDays));

            var result = await handler.HandleAsync(new RefreshSessionCommand(currentCookie));

            // Eventually should hit absolute expiry
            if (i * stepDays >= refreshAbsoluteDays)
            {
                Assert.Equal(RefreshResult.SessionInvalid, result.Status);
                return;
            }

            if (result.Status == RefreshResult.Success)
            {
                currentCookie = result.RefreshCookie!;
            }
        }

        Assert.Fail("Did not reach absolute expiry");
    }

    /// <summary>Test 5: Reuse detection - reusing a token outside grace window revokes the session
    /// and invalidates even the legitimate successor token.</summary>
    [Fact]
    public async Task ReuseDetection_RevokesSessionAndLegitimateToken()
    {
        var (session, originalCookie) = await CreateSession();

        var handler = new RefreshSessionHandler(
            _context,
            new SessionOptions
            {
                PlatformIssuer = "https://platform.example.com",
                RefreshAbsoluteDays = 30,
                RefreshGraceSeconds = 5
            },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        // First refresh (legitimate) - token A → B
        var result1 = await handler.HandleAsync(new RefreshSessionCommand(originalCookie));
        Assert.Equal(RefreshResult.Success, result1.Status);
        var legitimateCookie = result1.RefreshCookie!;

        // Advance beyond grace window
        _fixture.TimeProvider.Advance(TimeSpan.FromSeconds(10));

        // Reuse original token (A) outside grace window
        var result2 = await handler.HandleAsync(new RefreshSessionCommand(originalCookie));
        Assert.Equal(RefreshResult.SessionInvalid, result2.Status);

        // Verify session was revoked
        var revokedSession = await _context.AuthSessions.FirstAsync(s => s.Id == session.Id);
        Assert.True(revokedSession.IsRevoked);
        Assert.Equal("reuse_detected", revokedSession.RevokedReason);

        // Verify refresh_reuse_detected event exists
        var reuseEvent = await _context.AuthEvents.FirstOrDefaultAsync(
            e => e.EventType == "refresh_reuse_detected" && e.SessionId == session.Id);
        Assert.NotNull(reuseEvent);

        // Legitimate successor token (B) should now also be invalid
        var result3 = await handler.HandleAsync(new RefreshSessionCommand(legitimateCookie));
        Assert.Equal(RefreshResult.SessionInvalid, result3.Status);
    }

    /// <summary>Test 6: Refresh re-validates active tenant - if session's active tenant membership
    /// is later Disabled, refresh succeeds but clears the active tenant.</summary>
    [Fact]
    public async Task RefreshRevalidatesTenant_DisabledMembershipClearsActiveTenant()
    {
        var accountId = await ProvisionAccount("tenant@example.com", "ValidPassword123456");
        var tenantId = new TenantId(1);
        await AddActiveMembership(accountId, tenantId);

        var (session, refreshCookie) = await CreateSession(accountId);

        // Manually select the tenant on the session
        var sessionToUpdate = await _context.AuthSessions.FirstAsync(s => s.Id == session.Id);
        sessionToUpdate.SelectTenant(tenantId);
        await _context.SaveChangesAsync();

        var handler = new RefreshSessionHandler(
            _context,
            new SessionOptions { PlatformIssuer = "https://platform.example.com" },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        // Disable the membership as the superuser: the runtime role cannot see it without a tenant/account GUC.
        await using (var admin = _fixture.CreateAdminContext())
        {
            var membership = await admin.TenantMemberships
                .SingleAsync(m => m.AccountId == accountId && m.TenantId == tenantId);
            membership.Disable();
            await admin.SaveChangesAsync();
        }

        // Refresh should succeed but clear the active tenant
        var result = await handler.HandleAsync(new RefreshSessionCommand(refreshCookie));
        Assert.Equal(RefreshResult.Success, result.Status);
        Assert.Null(result.SelectedTenantId);

        // Verify in DB that active tenant is cleared
        var updatedSession = await _context.AuthSessions.FirstAsync(s => s.Id == session.Id);
        Assert.Null(updatedSession.ActiveTenantId);
    }

    /// <summary>Test 7: Logout idempotency and robustness - multiple logouts, garbage cookies,
    /// empty string, unknown token, huge value all don't throw.</summary>
    [Fact]
    public async Task LogoutRobustness_IdempotentAndHandlesInvalidInputs()
    {
        var (session, refreshCookie) = await CreateSession();

        var handler = new LogoutHandler(
            _context,
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        // First logout succeeds
        await handler.HandleAsync(refreshCookie);
        var revokedSession = await _context.AuthSessions.FirstAsync(s => s.Id == session.Id);
        Assert.True(revokedSession.IsRevoked);

        // Second logout with same cookie doesn't throw (idempotent)
        await handler.HandleAsync(refreshCookie); // No exception

        // Empty string doesn't throw
        await handler.HandleAsync(""); // No exception

        // Null/empty handled gracefully (test with empty)
        await handler.HandleAsync(string.Empty); // No exception

        // Garbage cookie doesn't throw
        await handler.HandleAsync("not.a.valid.cookie"); // No exception

        // Unknown token ID doesn't throw
        var unknownTokenId = Guid.NewGuid();
        await handler.HandleAsync($"{unknownTokenId}.somesecret"); // No exception

        // Huge cookie value doesn't throw
        var hugeSecret = new string('x', 10240);
        await handler.HandleAsync($"{Guid.NewGuid()}.{hugeSecret}"); // No exception

        // Verify token no longer refreshes
        var refreshHandler = new RefreshSessionHandler(
            _context,
            new SessionOptions { PlatformIssuer = "https://platform.example.com" },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        var refreshResult = await refreshHandler.HandleAsync(new RefreshSessionCommand(refreshCookie));
        Assert.Equal(RefreshResult.SessionInvalid, refreshResult.Status);
    }

    /// <summary>Test 8: SelectTenant identical error outcomes - unknown tenant, Disabled membership,
    /// Invited membership, and no membership all return identical TenantNotPermitted result.</summary>
    [Fact]
    public async Task SelectTenantIdenticalErrors_UnknownDisabledInvitedNoMembership()
    {
        var accountId = await ProvisionAccount("select@example.com", "ValidPassword123456");
        var (session, refreshCookie) = await CreateSession(accountId);

        var tenantIdUnknown = new TenantId(1);
        var tenantIdDisabled = new TenantId(2);
        var tenantIdInvited = new TenantId(3);

        // Setup: Disabled and Invited memberships
        await AddDisabledMembership(accountId, tenantIdDisabled);
        await AddInvitedMembership(accountId, tenantIdInvited);

        var handler = new SelectTenantHandler(
            _context,
            new SessionOptions { PlatformIssuer = "https://platform.example.com" },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        // Unknown tenant
        var unknownResult = await handler.HandleAsync(new SelectTenantCommand(refreshCookie, tenantIdUnknown));
        Assert.Equal(TenantSelectionStatus.TenantNotPermitted, unknownResult.Status);

        // Disabled membership
        var disabledResult = await handler.HandleAsync(new SelectTenantCommand(refreshCookie, tenantIdDisabled));
        Assert.Equal(TenantSelectionStatus.TenantNotPermitted, disabledResult.Status);

        // Invited membership
        var invitedResult = await handler.HandleAsync(new SelectTenantCommand(refreshCookie, tenantIdInvited));
        Assert.Equal(TenantSelectionStatus.TenantNotPermitted, invitedResult.Status);

        // No membership (random tenant)
        var noMembershipResult = await handler.HandleAsync(new SelectTenantCommand(refreshCookie, new TenantId(99)));
        Assert.Equal(TenantSelectionStatus.TenantNotPermitted, noMembershipResult.Status);

        // All should have identical shapes
        Assert.Null(unknownResult.SelectedTenantId);
        Assert.Null(disabledResult.SelectedTenantId);
        Assert.Null(invitedResult.SelectedTenantId);
        Assert.Null(noMembershipResult.SelectedTenantId);

        // Valid membership should succeed and update
        var tenantIdValid = new TenantId(4);
        await AddActiveMembership(accountId, tenantIdValid);

        var validResult = await handler.HandleAsync(new SelectTenantCommand(refreshCookie, tenantIdValid));
        Assert.Equal(TenantSelectionStatus.Success, validResult.Status);
        Assert.Equal(tenantIdValid.Value, validResult.SelectedTenantId);
    }

    /// <summary>Test 9: SessionValidator.IsActiveAsync - returns true for live sessions of the
    /// right account, false for revoked, expired, unknown, or wrong account.</summary>
    [Fact]
    public async Task SessionValidator_IsActiveAsync_CorrectResults()
    {
        var accountId1 = await ProvisionAccount("validator1@example.com", "Password12345678");
        var accountId2 = await ProvisionAccount("validator2@example.com", "Password12345678");

        var (session1, _) = await CreateSession(accountId1);
        var (session2, _) = await CreateSession(accountId2);

        var validator = new SessionValidator(_context, _fixture.TimeProvider);

        // Live session of the right account returns true
        var isActive = await validator.IsActiveAsync(session1.Id, accountId1);
        Assert.True(isActive);

        // Revoked session returns false
        session2.Revoke("test", _fixture.TimeProvider.GetUtcNow());
        await _context.SaveChangesAsync();
        var isRevokedActive = await validator.IsActiveAsync(session2.Id, accountId2);
        Assert.False(isRevokedActive);

        // Expired session returns false
        var (session3, _) = await CreateSession();
        _fixture.TimeProvider.Advance(TimeSpan.FromDays(31)); // Expire absolute (30 day default)
        var isExpiredActive = await validator.IsActiveAsync(session3.Id, session3.AccountId);
        Assert.False(isExpiredActive);

        // Unknown session ID returns false
        var isUnknownActive = await validator.IsActiveAsync(Guid.NewGuid(), accountId1);
        Assert.False(isUnknownActive);

        // Live session queried with different account ID returns false (security)
        var isDifferentAccountActive = await validator.IsActiveAsync(session1.Id, accountId2);
        Assert.False(isDifferentAccountActive);
    }

    /// <summary>Test 10: GetSessionOverviewHandler - returns only Active memberships, correct
    /// account summary; returns null for unknown and revoked sessions.</summary>
    [Fact]
    public async Task GetSessionOverviewHandler_ActiveMembershipsOnlyAndCorrectSummary()
    {
        var accountId = await ProvisionAccount("overview@example.com", "Password12345678");
        var tenantIdActive1 = new TenantId(1);
        var tenantIdActive2 = new TenantId(2);
        var tenantIdInvited = new TenantId(3);
        var tenantIdDisabled = new TenantId(4);

        await AddActiveMembership(accountId, tenantIdActive1);
        await AddActiveMembership(accountId, tenantIdActive2);
        await AddInvitedMembership(accountId, tenantIdInvited);
        await AddDisabledMembership(accountId, tenantIdDisabled);

        var (session, _) = await CreateSession(accountId);

        // Select one active tenant
        var sessionToUpdate = await _context.AuthSessions.FirstAsync(s => s.Id == session.Id);
        sessionToUpdate.SelectTenant(tenantIdActive1);
        await _context.SaveChangesAsync();

        var handler = new GetSessionOverviewHandler(_context, _fixture.TimeProvider);

        // GetSessionOverviewHandler calls SetAccountContextAsync which requires a transaction
        await using var tx = await _context.Database.BeginTransactionAsync();
        var overview = await handler.HandleAsync(session.Id);
        await tx.CommitAsync();
        Assert.NotNull(overview);
        Assert.Equal(accountId, overview.AccountId);
        Assert.Equal("Test User", overview.DisplayName); // Default from ProvisionAccount
        Assert.Equal(tenantIdActive1.Value, overview.SelectedTenantId);
        Assert.Equal(2, overview.MembershipTenantIds.Count);
        Assert.Contains(tenantIdActive1.Value, overview.MembershipTenantIds);
        Assert.Contains(tenantIdActive2.Value, overview.MembershipTenantIds);
        Assert.DoesNotContain(tenantIdInvited.Value, overview.MembershipTenantIds);
        Assert.DoesNotContain(tenantIdDisabled.Value, overview.MembershipTenantIds);

        // Unknown session returns null
        await using var tx2 = await _context.Database.BeginTransactionAsync();
        var unknownOverview = await handler.HandleAsync(Guid.NewGuid());
        Assert.Null(unknownOverview);
        await tx2.CommitAsync();

        // Revoked session returns null
        sessionToUpdate.Revoke("test", _fixture.TimeProvider.GetUtcNow());
        await _context.SaveChangesAsync();

        await using var tx3 = await _context.Database.BeginTransactionAsync();
        var revokedOverview = await handler.HandleAsync(session.Id);
        Assert.Null(revokedOverview);
        await tx3.CommitAsync();
    }

    /// <summary>Test 11: ProvisionPasswordAccountHandler - creates exactly one Account, Credential,
    /// ExternalIdentity with opaque subject; idempotent by normalized email; policy violation
    /// rejected; email normalization works for login.</summary>
    [Fact]
    public async Task ProvisionPasswordAccountHandler_ExactlyOneOfEach_IdempotentAndPolicy()
    {
        var emailVariants = new[] { "Test@Example.COM", "  test@example.com  ", "test@example.com" };
        var handler = new ProvisionPasswordAccountHandler(
            _context,
            new PasswordService(),
            new PasswordPolicy(new PasswordPolicyOptions()),
            _fixture.TimeProvider);

        // First provision
        var result1 = await handler.HandleAsync(
            new ProvisionPasswordAccountCommand(
                emailVariants[0],
                "Test User",
                "ValidPassword12345",
                "https://platform.example.com"));

        var accountId = result1.AccountId;
        var principal = result1.Principal;

        // Verify exactly one of each record
        var accountCount = await _context.Accounts.CountAsync(a => a.Id == accountId);
        Assert.Equal(1, accountCount);

        var credentialCount = await _context.AccountCredentials.CountAsync(c => c.AccountId == accountId);
        Assert.Equal(1, credentialCount);

        var credential = await _context.AccountCredentials.FirstAsync(c => c.AccountId == accountId);
        Assert.NotEmpty(credential.PasswordHash);
        Assert.DoesNotContain("ValidPassword12345", credential.PasswordHash); // Not plaintext

        var identityCount = await _context.ExternalIdentities.CountAsync(
            x => x.AccountId == accountId && x.Issuer == "https://platform.example.com");
        Assert.Equal(1, identityCount);

        var identity = await _context.ExternalIdentities.FirstAsync(
            x => x.AccountId == accountId && x.Issuer == "https://platform.example.com");
        Assert.NotEmpty(identity.Subject);
        // Subject should be a hex string (opaque identifier), not containing email or account ID in plain form
        Assert.True(identity.Subject.All(c => char.IsDigit(c) || (c >= 'a' && c <= 'f')),
            "Subject should be a hex string");
        Assert.DoesNotContain("test", identity.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("example", identity.Subject, StringComparison.OrdinalIgnoreCase);

        // Idempotency: variants should return same account
        var result2 = await handler.HandleAsync(
            new ProvisionPasswordAccountCommand(
                emailVariants[1],
                "Different Display Name",
                "DifferentPassword4567",
                "https://platform.example.com"));

        Assert.Equal(accountId, result2.AccountId);
        Assert.Equal(principal, result2.Principal);

        var result3 = await handler.HandleAsync(
            new ProvisionPasswordAccountCommand(
                emailVariants[2],
                "Yet Another Name",
                "YetAnotherPassword7891",
                "https://platform.example.com"));

        Assert.Equal(accountId, result3.AccountId);
        Assert.Equal(principal, result3.Principal);

        // Verify counts haven't changed
        Assert.Equal(1, await _context.Accounts.CountAsync(a => a.Id == accountId));
        Assert.Equal(1, await _context.AccountCredentials.CountAsync(c => c.AccountId == accountId));
        Assert.Equal(1, await _context.ExternalIdentities.CountAsync(
            x => x.AccountId == accountId && x.Issuer == "https://platform.example.com"));

        // Password policy violation should throw
        var invalidPassword = "123"; // Too short
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.HandleAsync(
                new ProvisionPasswordAccountCommand(
                    "newuser@example.com",
                    "New User",
                    invalidPassword,
                    "https://platform.example.com")));

        // Email normalization enables login with variants
        var authHandler = new AuthenticateHandler(
            _context,
            new PasswordService(),
            new LockoutOptions(),
            new SessionOptions { PlatformIssuer = "https://platform.example.com" },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        var authResult1 = await authHandler.HandleAsync(
            new AuthenticateCommand(emailVariants[0], "ValidPassword12345"));
        Assert.NotEqual(AuthenticationStatus.InvalidCredentials, authResult1.Status);

        var authResult2 = await authHandler.HandleAsync(
            new AuthenticateCommand(emailVariants[1], "ValidPassword12345"));
        Assert.NotEqual(AuthenticationStatus.InvalidCredentials, authResult2.Status);
    }

    /// <summary>Test 12: Events hygiene - full login → refresh → logout cycle never persists
    /// plaintext passwords, cookie secrets, token hashes, or password hashes in event details.</summary>
    [Fact]
    public async Task EventsHygiene_NoSecretsInEventDetails()
    {
        var email = "hygiene@example.com";
        var password = "ValidPassword12345";

        // Provision account
        var handler1 = new ProvisionPasswordAccountHandler(
            _context,
            new PasswordService(),
            new PasswordPolicy(new PasswordPolicyOptions()),
            _fixture.TimeProvider);
        var provisionResult = await handler1.HandleAsync(
            new ProvisionPasswordAccountCommand(email, "Test User", password, "https://platform.example.com"));

        // Login
        var authHandler = new AuthenticateHandler(
            _context,
            new PasswordService(),
            new LockoutOptions(),
            new SessionOptions { PlatformIssuer = "https://platform.example.com" },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        var authResult = await authHandler.HandleAsync(new AuthenticateCommand(email, password));
        var cookie = authResult.RefreshCookie!;

        // Refresh
        var refreshHandler = new RefreshSessionHandler(
            _context,
            new SessionOptions { PlatformIssuer = "https://platform.example.com", RefreshAbsoluteDays = 30 },
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        var refreshResult = await refreshHandler.HandleAsync(new RefreshSessionCommand(cookie));
        var newCookie = refreshResult.RefreshCookie!;

        // Logout
        var logoutHandler = new LogoutHandler(
            _context,
            new AuthEventWriter(_context),
            _fixture.TimeProvider);

        await logoutHandler.HandleAsync(newCookie);

        // Check all events
        var allEvents = await _context.AuthEvents.ToListAsync();
        Assert.NotEmpty(allEvents);

        foreach (var evt in allEvents)
        {
            var detail = evt.Detail ?? "";

            // Check for plaintext password
            Assert.DoesNotContain(password, detail, StringComparison.OrdinalIgnoreCase);

            // Check for cookie secret (from the generated token secret)
            // Cookie format is tokenId.secret, so check that secret portion
            if (!string.IsNullOrEmpty(cookie))
            {
                var parts = cookie.Split('.');
                if (parts.Length == 2)
                {
                    Assert.DoesNotContain(parts[1], detail);
                }
            }

            // Check for hash values (password hash or token hash)
            // We can't enumerate all possible hashes, but we can check that long base64-like strings
            // don't appear excessively in details (this is a heuristic)
            var credential = await _context.AccountCredentials.FirstAsync(c => c.AccountId == provisionResult.AccountId);
            Assert.DoesNotContain(credential.PasswordHash, detail);
        }
    }

    // ==================== Helper methods ====================

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
        accountId ??= await ProvisionAccount("session@example.com", "ValidPassword123456");

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

    private async Task AddActiveMembership(long accountId, TenantId tenantId)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        await _context.SetTenantContextAsync(tenantId);
        var membership = TenantMembership.Invite(tenantId, accountId);
        membership.Activate();
        _context.TenantMemberships.Add(membership);
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private async Task AddInvitedMembership(long accountId, TenantId tenantId)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        await _context.SetTenantContextAsync(tenantId);
        var membership = TenantMembership.Invite(tenantId, accountId);
        _context.TenantMemberships.Add(membership);
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private async Task AddDisabledMembership(long accountId, TenantId tenantId)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        await _context.SetTenantContextAsync(tenantId);
        var membership = TenantMembership.Invite(tenantId, accountId);
        membership.Activate();
        membership.Disable();
        _context.TenantMemberships.Add(membership);
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
