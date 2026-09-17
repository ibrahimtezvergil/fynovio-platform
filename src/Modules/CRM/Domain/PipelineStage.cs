using Contracts;

namespace CRM.Domain;

public sealed class PipelineStage
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public long PipelineDefinitionVersionId { get; private set; }
    public string Name { get; private set; } = null!;
    public int SortOrder { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private PipelineStage() { }

    internal static PipelineStage Create(TenantId tenantId, long pipelineDefinitionVersionId, string name, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (sortOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order cannot be negative.");

        return new PipelineStage
        {
            TenantId = tenantId,
            PipelineDefinitionVersionId = pipelineDefinitionVersionId,
            Name = name,
            SortOrder = sortOrder,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
