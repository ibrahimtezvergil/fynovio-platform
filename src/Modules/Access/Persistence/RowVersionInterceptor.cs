using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Access.Persistence;

/// <summary>Increments row_version on every modified IHasRowVersion entity before save.
/// Lives in this module rather than Contracts because Contracts may not depend on EF Core
/// (doc 08's "Contracts... no ORM" rule), and it is not shared with CRM because no module may
/// reference another module's namespace (AGENTS.md). Only the marker interface
/// (Contracts.IHasRowVersion) is shared. CRM no longer uses an interceptor — its Opportunity
/// increments its own version inside domain methods.</summary>
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
