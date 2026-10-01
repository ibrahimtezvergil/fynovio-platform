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

    // jsonb key-exists (`custom_fields ? key`); archived opportunities count too, since their values stay readable.
    private Task<int> CountOpportunitiesWithFieldAsync(TenantId tenantId, string fieldName, CancellationToken cancellationToken) =>
        context.Opportunities
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId && o.CustomFields != null && EF.Functions.JsonExists(o.CustomFields, fieldName))
            .CountAsync(cancellationToken);
}
