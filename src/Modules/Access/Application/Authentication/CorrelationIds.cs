namespace Access.Application.Authentication;

internal static class CorrelationIds
{
    /// <summary>The evidence/outbox tables need a Guid; a caller-supplied id that is not one is replaced, never rejected.</summary>
    public static Guid ParseOrNew(string? value) => Guid.TryParse(value, out var parsed) ? parsed : Guid.NewGuid();
}
