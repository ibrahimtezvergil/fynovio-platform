using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Collaboration.Persistence;

public static class CollaborationDbContextTenantExtensions
{
    public static async Task SetTenantContextAsync(
        this CollaborationDbContext context,
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Tenant context must be set inside an explicit transaction.");

        var value = tenantId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.tenant_id', {value}, true)",
            cancellationToken);
    }
}
