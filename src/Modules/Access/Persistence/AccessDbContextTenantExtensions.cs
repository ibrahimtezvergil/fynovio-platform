using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Persistence;

/// <summary>Writes the `app.tenant_id` setting the RLS policies read, scoped to the
/// current transaction (mirrors `CRM.Persistence.CrmDbContextTenantExtensions`).</summary>
public static class AccessDbContextTenantExtensions
{
    public static async Task SetTenantContextAsync(
        this AccessDbContext context,
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
