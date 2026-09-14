using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CRM.Persistence;

/// <summary>Increments row_version on every modified IHasRowVersion entity before save.
/// EF's concurrency-token mechanism handles the rest: it captures the property's original
/// value for the UPDATE ... WHERE clause and writes the new value, giving optimistic
/// concurrency without relying on Postgres's internal xmin.</summary>
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
