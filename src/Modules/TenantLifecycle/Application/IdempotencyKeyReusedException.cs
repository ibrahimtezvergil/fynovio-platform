namespace TenantLifecycle.Application;

public sealed class IdempotencyKeyReusedException(string operation, string key) : Exception($"Idempotency key '{key}' was already used for a different {operation} request.")
{
    public string Operation { get; } = operation;
    public string Key { get; } = key;
}
