using Contracts;
using Microsoft.EntityFrameworkCore;
using SemanticCatalog.Domain;
using SemanticCatalog.Persistence;

namespace SemanticCatalog.Application;

/// <summary>Implements the Contracts read side. It runs on the catalog's own connection, so it opens its own
/// transaction and sets `app.tenant_id` itself: under FORCE RLS an unset tenant context returns zero rows with no
/// error, which would read as "no definitions" to every consumer.</summary>
public sealed class SemanticDefinitionReader(SemanticCatalogDbContext context) : ISemanticDefinitionReader
{
    public async Task<IReadOnlyList<FieldDefinition>> ListFieldsAsync(TenantId tenantId, string ownerContext, string objectType, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(tenantId, cancellationToken);
        var rows = await context.FieldDefinitions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.OwnerContext == ownerContext && x.ObjectType == objectType)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Key)
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return rows.Select(row => row.ToReadModel()).ToList();
    }

    public async Task<FieldDefinition?> GetFieldAsync(TenantId tenantId, long definitionId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(tenantId, cancellationToken);
        var row = await context.FieldDefinitions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == definitionId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return row?.ToReadModel();
    }

    public async Task<IReadOnlyList<DependentView>> ListDependentViewsAsync(TenantId tenantId, long fieldDefinitionId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(tenantId, cancellationToken);
        var views = await (from edge in context.DependencyEdges.AsNoTracking()
                           join view in context.ViewDefinitions.AsNoTracking() on edge.FromId equals view.Id
                           where edge.TenantId == tenantId && edge.ToKind == DependencyEdge.FieldKind && edge.ToId == fieldDefinitionId && view.Status == FieldStatus.Active
                           orderby view.SortOrder, view.Key
                           select new DependentView(view.Id, view.Key, view.Name))
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return views;
    }
}
