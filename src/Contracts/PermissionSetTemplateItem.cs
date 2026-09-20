namespace Contracts;

/// <summary>One action a permission-set template grants. `Relation` is `null` (unrestricted
/// within the tenant) or `"owner"` — the same closed set `access.permission_set_items` accepts.</summary>
public sealed record PermissionSetTemplateItem(string ActionKey, string? Relation = null);
