using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Application;

public sealed class AccessActionCatalogService(AccessDbContext context) : IActionCatalog
{
    public async Task<bool> IsActiveAsync(ActionKey action, CancellationToken cancellationToken = default) =>
        await context.Actions.AnyAsync(a => a.ActionKey == action.Value && !a.IsDeprecated, cancellationToken);
}
