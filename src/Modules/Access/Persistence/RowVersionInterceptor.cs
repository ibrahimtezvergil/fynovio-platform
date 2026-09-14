using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Access.Persistence;

/// <summary>Increments row_version on every modified IHasRowVersion entity before save.
/// Identical mechanism to CRM.Persistence.RowVersionInterceptor — duplicated, not shared,
/// because Contracts may not depend on EF Core (doc 08's "Contracts... no ORM" rule) and no
/// module may reference another module's namespace (AGENTS.md's module boundary rule). Only
/// the marker interface (Contracts.IHasRowVersion) is actually shared.</summary>
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
