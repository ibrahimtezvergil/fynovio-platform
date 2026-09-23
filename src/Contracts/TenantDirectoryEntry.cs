namespace Contracts;

/// <summary>Non-sensitive tenant identity shown to an account that is already an active member.</summary>
public sealed record TenantDirectoryEntry(TenantId TenantId, string DisplayName);
