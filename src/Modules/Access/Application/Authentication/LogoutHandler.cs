using Access.Domain.Authentication;
using Access.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Access.Application.Authentication;

/// <summary>Idempotent logout: revokes a session by refresh token cookie value.</summary>
public sealed class LogoutHandler
{
    private readonly AccessDbContext _context;
    private readonly AuthEventWriter _eventWriter;
    private readonly TimeProvider _timeProvider;

    public LogoutHandler(AccessDbContext context, AuthEventWriter eventWriter, TimeProvider? timeProvider = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _eventWriter = eventWriter ?? throw new ArgumentNullException(nameof(eventWriter));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task HandleAsync(string cookieValue, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(cookieValue))
            return;

        var now = _timeProvider.GetUtcNow();
        var parts = cookieValue.Split('.');
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var tokenId))
            return;

        var token = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.Id == tokenId, cancellationToken);

        if (token is null)
            return;

        var session = await _context.AuthSessions
            .FirstOrDefaultAsync(s => s.Id == token.SessionId, cancellationToken);

        if (session is null || session.IsRevoked)
            return;

        session.Revoke("logout", now);
        await _context.SaveChangesAsync(cancellationToken);

        await _eventWriter.WriteAsync(
            "logout",
            "success",
            now,
            session.AccountId,
            sessionId: session.Id,
            correlationId: GetCorrelationId(),
            cancellationToken: cancellationToken);
    }

    private static string? GetCorrelationId()
    {
        return null;
    }
}
