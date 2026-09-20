namespace Contracts;

/// <summary>A reusable capability bundle a module ships. Enablement copies it into a
/// tenant-local `PermissionSet` (origin `system_template`); nothing keeps the copy in sync afterwards.</summary>
public sealed record PermissionSetTemplate(string Key, string Name, IReadOnlyList<PermissionSetTemplateItem> Items);
