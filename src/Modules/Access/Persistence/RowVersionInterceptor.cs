using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Access.Persistence;

/// <summary>Increments row_version on every modified IHasRowVersion entity before save.
/// Access uses an interceptor; CRM's Opportunity increments its own version inside domain
/// methods instead. Both approaches avoid xmin, using an explicit bigint concurrency token.
/// The marker interface (Contracts.IHasRowVersion) is shared; each module's implementation
/// strategy is independent to suit its boundary needs.</summary>
public sealed class RowVersionInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Apply(DbContext? context)
    {
        if (context is null)
            return;

        foreach (var entry in context.ChangeTracker.Entries<IHasRowVersion>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.IncrementRowVersion();
        }
    }
}
