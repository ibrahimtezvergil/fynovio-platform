using Access.Domain.Authentication;
using Access.Domain.Identity;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Application.Authentication;

/// <summary>Refresh a session using a rotating refresh token. Implements token rotation with
/// reuse detection: a rotated token presented again revokes the whole session family. Tokens
/// within the grace window return a conflict result without revocation.</summary>
public sealed class RefreshSessionHandler
{
    private readonly AccessDbContext _context;
    private readonly SessionOptions _sessionOptions;
    private readonly AuthEventWriter _eventWriter;
    private readonly TimeProvider _timeProvider;

    public RefreshSessionHandler(
        AccessDbContext context,
        SessionOptions sessionOptions,
        AuthEventWriter eventWriter,
        TimeProvider? timeProvider = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _sessionOptions = sessionOptions ?? throw new ArgumentNullException(nameof(sessionOptions));
        _eventWriter = eventWriter ?? throw new ArgumentNullException(nameof(eventWriter));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<RefreshSessionResult> HandleAsync(
        RefreshSessionCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null)
            throw new ArgumentNullException(nameof(command));

        var now = _timeProvider.GetUtcNow();

        // Parse cookie value: <refreshTokenId>.<secret>
        var parts = command.CookieValue.Split('.');
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var tokenId) || string.IsNullOrEmpty(parts[1]))
        {
            return new RefreshSessionResult(RefreshResult.SessionInvalid);
        }

        var tokenSecret = parts[1];
        var tokenHash = TokenSecrets.HashToken(tokenSecret);

        // Lookup token
        var token = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.Id == tokenId, cancellationToken);

        if (token is null)
        {
            return new RefreshSessionResult(RefreshResult.SessionInvalid);
        }

        // Constant-time hash comparison
        if (!TokenSecrets.HashesEqual(token.TokenHash, tokenHash))
        {
            return new RefreshSessionResult(RefreshResult.SessionInvalid);
        }

        // Check expiry
        if (token.IsExpired(now))
        {
            return new RefreshSessionResult(RefreshResult.SessionInvalid);
        }

        // Load session
        var session = await _context.AuthSessions
            .FirstOrDefaultAsync(s => s.Id == token.SessionId, cancellationToken);

        if (session is null || session.IsRevoked || session.IsExpired(now))
        {
            return new RefreshSessionResult(RefreshResult.SessionInvalid);
        }

        // Reuse detection: if token is already rotated, check grace period
        if (token.IsRotated)
        {
            var gracePeriod = TimeSpan.FromSeconds(_sessionOptions.RefreshGraceSeconds);
            if (now.Subtract(token.RotatedAt!.Value) > gracePeriod)
            {
                // Outside grace window — revoke session
                session.Revoke("reuse_detected", now);
                await _context.SaveChangesAsync(cancellationToken);

                await _eventWriter.WriteAsync(
                    "refresh_reuse_detected",
                    "session_revoked",
                    now,
                    session.AccountId,
                    sessionId: session.Id,
                    correlationId: command.CorrelationId,
                    cancellationToken: cancellationToken);

                return new RefreshSessionResult(RefreshResult.SessionInvalid);
            }
            else
            {
                // Inside grace window — return conflict without revocation
                return new RefreshSessionResult(RefreshResult.RefreshConflict);
            }
        }

        // Load account and external identity
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == session.AccountId, cancellationToken);
        if (account is null)
            return new RefreshSessionResult(RefreshResult.SessionInvalid);

        var externalIdentity = await _context.ExternalIdentities
            .FirstOrDefaultAsync(
                x => x.AccountId == account.Id && x.Issuer == _sessionOptions.PlatformIssuer,
                cancellationToken);

        if (externalIdentity is null)
            return new RefreshSessionResult(RefreshResult.SessionInvalid);

        // Use transaction for rotation
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.SetAccountContextAsync(account.Id, cancellationToken);

        // Mark old token as rotated
        token.MarkRotated(Guid.NewGuid(), now);

        // Create new refresh token with sliding idle expiry
        var newTokenCreatedAt = now;
        var newIdleExpiry = newTokenCreatedAt.AddDays(_sessionOptions.RefreshIdleDays);
        var (newTokenSecret, newTokenHash) = TokenSecrets.GenerateAndHash();
        var newToken = RefreshToken.Create(session.Id, newTokenHash, newTokenCreatedAt, newIdleExpiry);
        _context.RefreshTokens.Add(newToken);

        // Update session last used and re-check active membership
        session.UpdateLastUsed(now);

        // If no active tenant selected, validate current selection still exists
        if (session.ActiveTenantId.HasValue)
        {
            var membershipExists = await _context.TenantMemberships
                .AnyAsync(m =>
                    m.AccountId == account.Id &&
                    m.TenantId == session.ActiveTenantId &&
                    m.Status == Domain.Identity.MembershipStatus.Active,
                    cancellationToken);

            if (!membershipExists)
            {
                session.ClearTenant();
            }
        }

        // Load active memberships
        var memberships = await _context.TenantMemberships
            .Where(m => m.AccountId == account.Id && m.Status == Domain.Identity.MembershipStatus.Active)
            .Select(m => m.TenantId.Value)
            .ToListAsync(cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        // Determine response: token is valid, always return Success.
        // The endpoint translates the membership state into authenticated/tenant_selection_required/no_membership
        long? selectedTenantId = session.ActiveTenantId?.Value;

        // If a tenant was selected but is no longer a member, clear it for the response
        if (selectedTenantId.HasValue && !memberships.Contains(selectedTenantId.Value))
        {
            selectedTenantId = null;
        }

        // Auto-select if exactly one active membership and none was previously selected
        if (!selectedTenantId.HasValue && memberships.Count == 1)
        {
            selectedTenantId = memberships[0];
        }

        var status = RefreshResult.Success;

        // Write event
        await _eventWriter.WriteAsync(
            "session_refreshed",
            status.ToString().ToLowerInvariant(),
            now,
            account.Id,
            sessionId: session.Id,
            correlationId: command.CorrelationId,
            cancellationToken: cancellationToken);

        var newRefreshCookieValue = $"{newToken.Id}.{newTokenSecret}";

        return new RefreshSessionResult(
            status == RefreshResult.SessionInvalid ? RefreshResult.SessionInvalid : RefreshResult.Success,
            account.Id,
            account.DisplayName,
            memberships,
            selectedTenantId,
            null, // AccessToken issued by Host
            newRefreshCookieValue,
            session.Id,
            externalIdentity.Principal);
    }
}
