namespace Collaboration.Application;

/// <summary>The caller reused an idempotency key for a request with a different payload —
/// a client bug, not a retry (doc 04's IdempotencyKey: same key binds the same input).</summary>
public sealed class IdempotencyKeyReusedException(string operation, string idempotencyKey)
    : Exception($"Idempotency key '{idempotencyKey}' for operation '{operation}' was reused with a different request.");
