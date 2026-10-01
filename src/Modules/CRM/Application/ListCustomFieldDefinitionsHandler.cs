using Contracts;
using CRM.Customization;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application;

public sealed record ListCustomFieldDefinitionsQuery(
    TenantId TenantId,
    PrincipalRef Principal,
    TenantFieldAggregateType AggregateType,
    Guid CorrelationId);

public sealed record CustomFieldDefinitionDto(
    long Id,
    string FieldName,
    string Label,
    string FieldType,
    bool IsRequired,
    CustomFieldConfigDto Config,
    string Status,
    int SortOrder,
    long RowVersion);

public sealed record CustomFieldConfigDto(
    IReadOnlyList<CustomFieldOptionDto>? Options,
    int? Scale,
    decimal? Min,
    decimal? Max,
    int? MaxLength);

public sealed record CustomFieldOptionDto(
    string Key,
    string Label,
    bool IsDeprecated);

public sealed class ListCustomFieldDefinitionsHandler(CrmDbContext context, IAuthorizer authorizer)
{
    public async Task<IReadOnlyList<CustomFieldDefinitionDto>> HandleAsync(ListCustomFieldDefinitionsQuery query, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(query.TenantId, cancellationToken);
        await GetCrmSettingsHandler.AuthorizeAsync(query.TenantId, query.Principal, query.CorrelationId, CrmActionKeys.SettingsRead, authorizer, cancellationToken);

        if (query.AggregateType == TenantFieldAggregateType.Party)
            throw new ArgumentException("Party field definitions are not supported in this release.");

        var definitions = await context.TenantFieldDefinitions
            .AsNoTracking()
            .Where(x => x.TenantId == query.TenantId && x.AggregateType == query.AggregateType)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.FieldName)
            .ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return definitions.Select(d => MapToDto(d)).ToList();
    }

    private static CustomFieldDefinitionDto MapToDto(TenantFieldDefinition definition)
    {
        var config = definition.Config;
        return new CustomFieldDefinitionDto(
            definition.Id,
            definition.FieldName,
            definition.Label,
            TenantFieldValueTypeNames.ToName(definition.FieldType),
            definition.IsRequired,
            new CustomFieldConfigDto(
                config.Options?.Select(o => new CustomFieldOptionDto(o.Key, o.Label, o.IsDeprecated)).ToList(),
                config.Scale,
                config.Min,
                config.Max,
                config.MaxLength),
            definition.Status.ToString(),
            definition.SortOrder,
            definition.RowVersion);
    }
}
