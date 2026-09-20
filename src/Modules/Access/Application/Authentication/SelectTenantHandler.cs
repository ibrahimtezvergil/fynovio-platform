using Access.Domain.Identity;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Application.Authentication;

/// <summary>Select an active tenant for a session (via refresh cookie). Validates that the
/// session belongs to an account with an active membership in the target tenant. Non-member
/// and unknown-tenant return the same error to prevent tenant enumeration.</summary>
public sealed class SelectTenantHandler
{
    private readonly AccessDbContext _context;
    private readonly SessionOptions _sessionOptions;
    private readonly AuthEventWriter _eventWriter;
    private readonly TimeProvider _timeProvider;

    public SelectTenantHandler(
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

    public async Task<SelectTenantResult> HandleAsync(
        SelectTenantCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null)
            throw new ArgumentNullException(nameof(command));

        var now = _timeProvider.GetUtcNow();
        var parts = command.CookieValue.Split('.');
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var tokenId) || string.IsNullOrEmpty(parts[1]))
            return new SelectTenantResult(TenantSelectionStatus.SessionInvalid);

        var tokenSecret = parts[1];
        var tokenHash = TokenSecrets.HashToken(tokenSecret);

        // Lookup token and session
        var token = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.Id == tokenId, cancellationToken);

        if (token is null)
            return new SelectTenantResult(TenantSelectionStatus.SessionInvalid);

        // Verify secret with constant-time comparison
        if (!TokenSecrets.HashesEqual(token.TokenHash, tokenHash))
            return new SelectTenantResult(TenantSelectionStatus.SessionInvalid);

        // Token must not be rotated (only active tokens are session credentials)
        if (token.IsRotated)
            return new SelectTenantResult(TenantSelectionStatus.SessionInvalid);

        var session = await _context.AuthSessions
            .FirstOrDefaultAsync(s => s.Id == token.SessionId, cancellationToken);

        if (session is null || session.IsRevoked || session.IsExpired(now))
            return new SelectTenantResult(TenantSelectionStatus.SessionInvalid);

        // Use transaction to validate membership and set tenant context
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.SetAccountContextAsync(session.AccountId, cancellationToken);

        // Validate membership (inside transaction with account context set for RLS)
        var membership = await _context.TenantMemberships
            .FirstOrDefaultAsync(m =>
                m.AccountId == session.AccountId &&
                m.TenantId == command.TenantId &&
                m.Status == MembershipStatus.Active,
                cancellationToken);

        if (membership is null)
        {
            // Rollback transaction
            await tx.RollbackAsync(cancellationToken);

            // Same error for unknown tenant and non-member to prevent enumeration
            await _eventWriter.WriteAsync(
                "tenant_selected",
                "tenant_not_permitted",
                now,
                session.AccountId,
                sessionId: session.Id,
                correlationId: command.CorrelationId,
                ipHash: command.IpHash,
                cancellationToken: cancellationToken);

            return new SelectTenantResult(TenantSelectionStatus.TenantNotPermitted);
        }

        // Set tenant context for the session update
        await _context.SetTenantContextAsync(command.TenantId, cancellationToken);

        // Update session active tenant
        session.SelectTenant(command.TenantId);
        session.UpdateLastUsed(now);

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        var externalIdentity = await _context.ExternalIdentities
            .FirstOrDefaultAsync(
                x => x.AccountId == session.AccountId && x.Issuer == _sessionOptions.PlatformIssuer,
                cancellationToken);

        if (externalIdentity is null)
            return new SelectTenantResult(TenantSelectionStatus.SessionInvalid);

        // Write event
        await _eventWriter.WriteAsync(
            "tenant_selected",
            "success",
            now,
            session.AccountId,
            command.TenantId.Value,
            session.Id,
            correlationId: command.CorrelationId,
                ipHash: command.IpHash,
            cancellationToken: cancellationToken);

        return new SelectTenantResult(
            TenantSelectionStatus.Success,
            command.TenantId.Value,
            null, // AccessToken issued by Host
            session.Id,
            externalIdentity.Principal);
    }
}
