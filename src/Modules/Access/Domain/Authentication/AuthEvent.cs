namespace Access.Domain.Authentication;

/// <summary>
/// Append-only security event log. Events record authentication outcomes
/// (login success/failure, token events, password changes, etc.), lockouts,
/// and reuse detection. Includes audit trail and observability for security monitoring.
/// Platform-global to capture pre-tenant events (login, password reset requests).
/// </summary>
public sealed class AuthEvent
{
    public long Id { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string EventType { get; private set; } = null!;
    public long? AccountId { get; private set; }
    public long? TenantId { get; private set; }
    public Guid? SessionId { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? IpHash { get; private set; }
    public string Outcome { get; private set; } = null!;
    public string? Detail { get; private set; }

    private AuthEvent() { }

    public static AuthEvent Create(
        string eventType,
        string outcome,
        DateTimeOffset occurredAt,
        long? accountId = null,
        long? tenantId = null,
        Guid? sessionId = null,
        string? correlationId = null,
        string? ipHash = null,
        string? detail = null)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("Event type is required.", nameof(eventType));
        if (string.IsNullOrWhiteSpace(outcome))
            throw new ArgumentException("Outcome is required.", nameof(outcome));

        return new AuthEvent
        {
            OccurredAt = occurredAt,
            EventType = eventType,
            AccountId = accountId,
            TenantId = tenantId,
            SessionId = sessionId,
            CorrelationId = correlationId,
            IpHash = ipHash,
            Outcome = outcome,
            Detail = detail
        };
    }
}
