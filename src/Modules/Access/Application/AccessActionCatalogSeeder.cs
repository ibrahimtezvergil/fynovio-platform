using Access.Domain.Authorization;
using Access.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Access.Application;

/// <summary>Idempotent upsert, deprecate-not-delete (gap-closure §3). Runs once at
/// Host startup, no admin UI, no reconciler loop. Accepts the manifest from the
/// caller rather than hardcoding AccessActionCatalog.All, because Phase 2 needs to
/// seed CRM's action keys too, and Access must not reference CRM to do it — Host
/// composes both manifests and passes the union here.</summary>
public static class AccessActionCatalogSeeder
{
    public static async Task EnsureSeededAsync(
        AccessDbContext context,
        IEnumerable<ActionRegistryDescriptor> manifest,
        CancellationToken cancellationToken = default)
    {
        var existing = await context.Actions.ToDictionaryAsync(a => a.ActionKey, cancellationToken);
        var descriptors = manifest.ToList();
        var manifestKeys = descriptors.Select(d => d.ActionKey).ToHashSet();

        foreach (var descriptor in descriptors)
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
