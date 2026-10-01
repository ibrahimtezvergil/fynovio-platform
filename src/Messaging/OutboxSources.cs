namespace Messaging;

/// <summary>Every module schema with an <c>outbox_messages</c> table (E-6). The only schema names ever interpolated
/// into SQL — callers pass one of these, and <see cref="Require"/> rejects anything else.</summary>
public static class OutboxSources
{
    public static readonly IReadOnlyList<string> All = ["crm", "masterdata", "access", "collaboration", "tenant_lifecycle"];

    public static string Require(string schema) =>
        All.Contains(schema) ? schema : throw new ArgumentOutOfRangeException(nameof(schema), schema, "Not an outbox source.");
}
