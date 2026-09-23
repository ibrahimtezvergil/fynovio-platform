using Contracts;
using Microsoft.EntityFrameworkCore;
using TenantLifecycle.Persistence;

namespace TenantLifecycle.Application;

/// <summary>Resolves display names for tenant IDs whose membership was already verified by the Host's Access query.</summary>
public sealed class TenantDirectory(TenantLifecycleDbContext context) : ITenantDirectory
{
    public async Task<IReadOnlyList<TenantDirectoryEntry>> GetEntriesAsync(
        IReadOnlyList<TenantId> tenantIds,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<TenantDirectoryEntry>();
        var seen = new HashSet<TenantId>();

        foreach (var tenantId in tenantIds)
        {
            if (!seen.Add(tenantId))
                continue;

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await context.SetTenantContextAsync(tenantId, cancellationToken);
            var displayName = await context.TenantProfiles.AsNoTracking()
                .Where(profile => profile.TenantId == tenantId)
                .Select(profile => profile.DisplayName)
                .SingleOrDefaultAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            if (displayName is not null)
                entries.Add(new TenantDirectoryEntry(tenantId, displayName));
        }

        return entries;
    }
}
