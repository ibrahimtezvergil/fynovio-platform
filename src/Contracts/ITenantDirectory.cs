namespace Contracts;

/// <summary>Public tenant-name query used by the Host after Access has established the caller's active memberships.</summary>
public interface ITenantDirectory
{
    Task<IReadOnlyList<TenantDirectoryEntry>> GetEntriesAsync(
        IReadOnlyList<TenantId> tenantIds,
        CancellationToken cancellationToken = default);
}
