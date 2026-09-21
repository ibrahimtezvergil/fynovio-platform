using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Collaboration.Persistence;

public static class CollaborationDbContextTenantExtensions
{
    public static Task SetTenantContextAsync(this CollaborationDbContext context, TenantId tenantId, CancellationToken cancellationToken = default) =>
        context.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('app.tenant_id', {tenantId.Value.ToString()}, true)", cancellationToken);
}
