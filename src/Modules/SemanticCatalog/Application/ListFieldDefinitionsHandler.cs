using Contracts;
using Microsoft.EntityFrameworkCore;
using SemanticCatalog.Persistence;

namespace SemanticCatalog.Application;

public sealed record ListFieldDefinitionsQuery(TenantId TenantId, PrincipalRef Principal, string OwnerContext, string ObjectType, Guid CorrelationId);

public sealed record FieldDefinitionDto(
    long Id,
    string FieldName,
    string Label,
    string FieldType,
    bool IsRequired,
    FieldConfigDto Config,
    string Status,
    int SortOrder,
    long RowVersion);

public sealed record FieldConfigDto(IReadOnlyList<FieldOptionDto>? Options, int? Scale, decimal? Min, decimal? Max, int? MaxLength, FieldTargetDto? Target = null);

public sealed record FieldTargetDto(string BoundedContext, string EntityType);

public sealed record FieldOptionDto(string Key, string Label, bool IsDeprecated);

public sealed class ListFieldDefinitionsHandler(SemanticCatalogDbContext context, IAuthorizer authorizer)
{
    public async Task<IReadOnlyList<FieldDefinitionDto>> HandleAsync(ListFieldDefinitionsQuery query, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(query.TenantId, cancellationToken);
        await CatalogAuthorization.AuthorizeAsync(query.TenantId, query.Principal, query.CorrelationId, query.OwnerContext, write: false, authorizer, cancellationToken);

        var definitions = await context.FieldDefinitions
            .AsNoTracking()
            .Where(x => x.TenantId == query.TenantId && x.OwnerContext == query.OwnerContext && x.ObjectType == query.ObjectType)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Key)
            .ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return definitions.Select(d => MapToDto(d.ToReadModel())).ToList();
    }

    internal static FieldDefinitionDto MapToDto(FieldDefinition definition) => new(
        definition.Id,
        definition.Key,
        definition.Label,
        FieldTypeNames.ToName(definition.Type),
        definition.IsRequired,
        new FieldConfigDto(
            definition.Config.Options?.Select(o => new FieldOptionDto(o.Key, o.Label, o.IsDeprecated)).ToList(),
            definition.Config.Scale,
            definition.Config.Min,
            definition.Config.Max,
            definition.Config.MaxLength,
            definition.Config.Target is null ? null : new FieldTargetDto(definition.Config.Target.BoundedContext, definition.Config.Target.EntityType)),
        definition.Status.ToString(),
        definition.SortOrder,
        definition.RowVersion);
}
