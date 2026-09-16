namespace MasterData.Application;

public sealed class IdempotencyKeyReusedException : InvalidOperationException
{
    public IdempotencyKeyReusedException(string operation, string idempotencyKey)
        : base($"Idempotency key '{idempotencyKey}' was already used for a different {operation} request.")
    {
    }
}
