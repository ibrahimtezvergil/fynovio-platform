using Contracts;

namespace TenantLifecycle.Idempotency;

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
        string key,
        string hash,
        int status,
        string response,
        TimeSpan retention) =>
        new()
        {
            TenantId = tenantId,
            PrincipalIssuer = principal.Issuer,
            PrincipalSubject = principal.Subject,
            Operation = operation,
            IdempotencyKey = key,
            RequestHash = hash,
            ResponseStatus = status,
            ResponsePayload = response,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(retention)
        };
}
