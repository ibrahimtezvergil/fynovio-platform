namespace Access.Domain.Authentication;

/// <summary>
/// Rotating refresh token within an authentication session. Implements token rotation
/// with reuse detection: each refresh issues a new token and marks the previous one as
/// rotated. Presenting a token that already has a RotatedAt value (outside grace window)
/// indicates reuse and triggers session revocation.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RotatedAt { get; private set; }
    public Guid? ReplacedById { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Create(Guid sessionId, string tokenHash, DateTimeOffset issuedAt, DateTimeOffset expiresAt, Guid? id = null)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("Session ID cannot be empty.", nameof(sessionId));
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        if (expiresAt <= issuedAt)
            throw new ArgumentException("Expiry must be after issuance.", nameof(expiresAt));

        return new RefreshToken
        {
            Id = id ?? Guid.NewGuid(),
            SessionId = sessionId,
            TokenHash = tokenHash,
            IssuedAt = issuedAt,
            ExpiresAt = expiresAt
        };
    }

    public void MarkRotated(Guid replacedById, DateTimeOffset now)
    {
        if (RotatedAt.HasValue)
            throw new InvalidOperationException("Token is already marked as rotated.");
        if (replacedById == Guid.Empty)
            throw new ArgumentException("Replaced by ID cannot be empty.", nameof(replacedById));

        RotatedAt = now;
        ReplacedById = replacedById;
    }

    public bool IsExpired(DateTimeOffset now)
    {
        return now >= ExpiresAt;
    }

    public bool IsRotated => RotatedAt.HasValue;
}
