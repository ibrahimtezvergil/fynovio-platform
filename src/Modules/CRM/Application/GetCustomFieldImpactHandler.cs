using Contracts;
using CRM.Customization;
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

public sealed class GetCustomFieldImpactHandler(CrmDbContext context, IAuthorizer authorizer, ISemanticDefinitionReader definitionReader)
{
    public async Task<CustomFieldImpactDto> HandleAsync(GetCustomFieldImpactQuery query, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(query.TenantId, cancellationToken);
        await GetCrmSettingsHandler.AuthorizeAsync(query.TenantId, query.Principal, query.CorrelationId, CrmActionKeys.SettingsUpdate, authorizer, cancellationToken);

        var definition = await definitionReader.GetFieldAsync(query.TenantId, query.DefinitionId, cancellationToken);
        if (definition is not { OwnerContext: OpportunityFields.OwnerContext, ObjectType: OpportunityFields.ObjectType })
            throw new KeyNotFoundException($"Field definition {query.DefinitionId} was not found.");

        var opportunitiesWithValue = await CountOpportunitiesWithFieldAsync(query.TenantId, definition.Key, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new CustomFieldImpactDto(definition.Id, definition.Key, opportunitiesWithValue);
    }

    // jsonb key-exists (`custom_fields ? key`); archived opportunities count too, since their values stay readable.
    private Task<int> CountOpportunitiesWithFieldAsync(TenantId tenantId, string fieldName, CancellationToken cancellationToken) =>
        context.Opportunities
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId && o.CustomFields != null && EF.Functions.JsonExists(o.CustomFields, fieldName))
            .CountAsync(cancellationToken);
}
