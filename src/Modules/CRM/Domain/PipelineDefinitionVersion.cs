using Contracts;

namespace CRM.Domain;

public sealed class PipelineDefinitionVersion
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public long PipelineDefinitionId { get; private set; }
    public int VersionNumber { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<PipelineStage> _stages = [];
    public IReadOnlyCollection<PipelineStage> Stages => _stages;

    private PipelineDefinitionVersion() { }

    internal static PipelineDefinitionVersion Create(TenantId tenantId, int versionNumber)
    {
        if (versionNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(versionNumber), "Version number must be positive.");

        return new PipelineDefinitionVersion
        {
            TenantId = tenantId,
            VersionNumber = versionNumber,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public PipelineStage AddStage(string name, int sortOrder)
    {
        if (_stages.Any(s => s.SortOrder == sortOrder))
            throw new InvalidOperationException($"Sort order {sortOrder} is already used on this version.");

        var stage = PipelineStage.Create(TenantId, name, sortOrder);
        _stages.Add(stage);
        return stage;
    }
}
