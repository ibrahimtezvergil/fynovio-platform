namespace CRM.Application;

/// <summary>The caller reused an idempotency key for a request with a different payload —
/// a client bug, not a retry (doc 04's IdempotencyKey: same key binds the same input).</summary>
public sealed class IdempotencyKeyReusedException : InvalidOperationException
{
    public IdempotencyKeyReusedException(string operation, string idempotencyKey)
        : base($"Idempotency key '{idempotencyKey}' was already used for a different {operation} request.")
    {
    }
}
