using Contracts;
using Microsoft.EntityFrameworkCore;

namespace SemanticCatalog.Persistence;

/// <summary>Writes the `app.tenant_id` setting the RLS policies read, scoped to the current
/// transaction (set_config's third argument is `true`), so a pooled connection never carries
/// one tenant's context into the next request (doc 07 §3). Without an explicit transaction
/// the setting would outlive the statement, so the call refuses to run.</summary>
public static class SemanticCatalogDbContextTenantExtensions
{
    public static async Task SetTenantContextAsync(
        this SemanticCatalogDbContext context,
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
