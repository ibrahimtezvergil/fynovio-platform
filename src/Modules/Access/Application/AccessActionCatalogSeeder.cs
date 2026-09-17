using Access.Domain.Authorization;
using Access.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Access.Application;

/// <summary>Idempotent upsert, deprecate-not-delete (gap-closure §3). Runs once at
/// Host startup, no admin UI, no reconciler loop.</summary>
public static class AccessActionCatalogSeeder
{
    public static async Task EnsureSeededAsync(AccessDbContext context, CancellationToken cancellationToken = default)
    {
        var existing = await context.Actions.ToDictionaryAsync(a => a.ActionKey, cancellationToken);
        var manifestKeys = AccessActionCatalog.All.Select(d => d.ActionKey).ToHashSet();

        foreach (var descriptor in AccessActionCatalog.All)
        {
            if (existing.TryGetValue(descriptor.ActionKey, out var entry))
            {
                if (entry.IsDeprecated)
                    entry.Reactivate();
                continue;
            }

            context.Actions.Add(ActionRegistryEntry.Create(
                descriptor.ActionKey, descriptor.OwnerModule, descriptor.ResourceType, descriptor.RiskClass));
        }

        foreach (var entry in existing.Values.Where(e => !manifestKeys.Contains(e.ActionKey) && !e.IsDeprecated))
            entry.Deprecate();

        await context.SaveChangesAsync(cancellationToken);
    }
}
