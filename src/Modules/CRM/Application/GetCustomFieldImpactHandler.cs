using Contracts;
using CRM.Domain;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application;

public sealed record GetCustomFieldImpactQuery(
    TenantId TenantId,
    PrincipalRef Principal,
    long DefinitionId,
    Guid CorrelationId);

public sealed record CustomFieldImpactDto(
    long DefinitionId,
    string FieldName,
    int OpportunitiesWithValue);

public sealed class GetCustomFieldImpactHandler(CrmDbContext context, IAuthorizer authorizer)
{
    public async Task<CustomFieldImpactDto> HandleAsync(GetCustomFieldImpactQuery query, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(query.TenantId, cancellationToken);
        await GetCrmSettingsHandler.AuthorizeAsync(query.TenantId, query.Principal, query.CorrelationId, CrmActionKeys.SettingsUpdate, authorizer, cancellationToken);

        var definition = await context.TenantFieldDefinitions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == query.TenantId && x.Id == query.DefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Field definition {query.DefinitionId} was not found.");

        var opportunitiesWithValue = await CountOpportunitiesWithFieldAsync(query.TenantId, definition.FieldName, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new CustomFieldImpactDto(definition.Id, definition.FieldName, opportunitiesWithValue);
    }

    private async Task<int> CountOpportunitiesWithFieldAsync(TenantId tenantId, string fieldName, CancellationToken cancellationToken)
    {
        // Try using EF.Functions.JsonExists if Npgsql supports it
        // Otherwise fall back to raw SQL with the ? operator
        try
        {
            var count = await context.Opportunities
                .AsNoTracking()
                .Where(o => o.TenantId == tenantId && o.CustomFields != null && EF.Functions.JsonExists(o.CustomFields, fieldName))
                .CountAsync(cancellationToken);
            return count;
        }
        catch
        {
            // Fallback to raw SQL using the jsonb ? operator
            var count = await context.Database
                .SqlQuery<int>($"SELECT COUNT(*) FROM crm.opportunities WHERE tenant_id = {tenantId.Value} AND custom_fields ? {fieldName}")
                .FirstOrDefaultAsync(cancellationToken);
            return count;
        }
    }
}
