using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Persistence;

/// <summary>Writes the `app.tenant_id` and `app.account_id` settings the RLS policies read,
/// scoped to the current transaction (mirrors `CRM.Persistence.CrmDbContextTenantExtensions`).</summary>
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

    /// <summary>Writes the `app.account_id` setting used by the `membership_self_view` RLS
    /// policy to allow authenticated users to view only their own membership rows across
    /// tenants. Must be called inside an explicit transaction. Used after account
    /// authentication (credential verify or refresh token validate) to enable the account to
    /// query its memberships before tenant selection.</summary>
    public static async Task SetAccountContextAsync(
        this AccessDbContext context,
        long accountId,
        CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Account context must be set inside an explicit transaction.");

        var value = accountId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.account_id', {value}, true)",
            cancellationToken);
    }
}
