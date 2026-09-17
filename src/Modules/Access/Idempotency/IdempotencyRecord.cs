using Contracts;

namespace Access.Idempotency;

/// <summary>Deliberately duplicated from `CRM.Idempotency.IdempotencyRecord`. Grant/
/// Revoke lookups happen AFTER authorization (round 3 §9/§13 final invariant).</summary>
public sealed class IdempotencyRecord
{
    public TenantId TenantId { get; private set; }
    public string PrincipalIssuer { get; private set; } = null!;
    public string PrincipalSubject { get; private set; } = null!;
    public string Operation { get; private set; } = null!;
    public string IdempotencyKey { get; private set; } = null!;
    public string RequestHash { get; private set; } = null!;
    public int ResponseStatus { get; private set; }
    public string ResponsePayload { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    private IdempotencyRecord() { }

    public static IdempotencyRecord Create(
        TenantId tenantId,
        PrincipalRef principal,
        string operation,
        string idempotencyKey,
        string requestHash,
        int responseStatus,
        string responsePayload,
        TimeSpan retention)
    {
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("Operation is required.", nameof(operation));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        if (string.IsNullOrWhiteSpace(requestHash))
            throw new ArgumentException("Request hash is required.", nameof(requestHash));

        var now = DateTimeOffset.UtcNow;
        return new IdempotencyRecord
        {
            TenantId = tenantId,
            PrincipalIssuer = principal.Issuer,
            PrincipalSubject = principal.Subject,
            Operation = operation,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash,
            ResponseStatus = responseStatus,
            ResponsePayload = responsePayload,
            CreatedAt = now,
            ExpiresAt = now.Add(retention)
        };
    }
}
