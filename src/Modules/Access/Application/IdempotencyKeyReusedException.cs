namespace Access.Application;

public sealed class IdempotencyKeyReusedException(string operation, string idempotencyKey)
    : Exception($"Idempotency key '{idempotencyKey}' for operation '{operation}' was reused with a different request.");
