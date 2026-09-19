using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application;

public sealed class GetPipelineStagesHandler(CrmDbContext context)
{
    public async Task<IReadOnlyList<PipelineStageDto>> HandleAsync(GetPipelineStagesQuery query, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(query.TenantId, cancellationToken);

        var results = await context.PipelineStages
            .Where(s => s.PipelineDefinitionVersionId == query.PipelineDefinitionVersionId)
            .OrderBy(s => s.SortOrder)
            .Select(s => new PipelineStageDto(s.Id, s.Name, s.SortOrder, s.IsActive, s.IsEntry))
            .ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return results;
    }
}
