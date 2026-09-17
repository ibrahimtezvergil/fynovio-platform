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

    internal static PipelineDefinitionVersion Create(TenantId tenantId, long pipelineDefinitionId, int versionNumber)
    {
        if (versionNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(versionNumber), "Version number must be positive.");

        return new PipelineDefinitionVersion
        {
            TenantId = tenantId,
            PipelineDefinitionId = pipelineDefinitionId,
            VersionNumber = versionNumber,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>Stamps the stage with this version's current `Id` — same save-before-add
    /// requirement as PipelineDefinition.AddVersion, for the same reason (`Stages` is
    /// EF-`Ignore()`d, no navigation-based fixup). The first stage added to a version
    /// becomes its entry stage by default (architecture plan §2.3 OD#2, option (i)) —
    /// callers that want a different entry stage call `MarkEntry` afterward.</summary>
    public PipelineStage AddStage(string name, int sortOrder)
    {
        if (_stages.Any(s => s.SortOrder == sortOrder))
            throw new InvalidOperationException($"Sort order {sortOrder} is already used on this version.");

        var stage = PipelineStage.Create(TenantId, Id, name, sortOrder, isEntry: _stages.Count == 0);
        _stages.Add(stage);
        return stage;
    }

    /// <summary>Enforces "exactly one entry stage per version" (architecture plan §2.3) —
    /// a cross-row invariant within this aggregate's own child collection, same pattern
    /// as Opportunity.Win()'s billable-line invariant.</summary>
    public void MarkEntry(PipelineStage stage)
    {
        if (!_stages.Contains(stage))
            throw new InvalidOperationException("Stage does not belong to this pipeline definition version.");

        foreach (var s in _stages)
            s.SetEntry(ReferenceEquals(s, stage));
    }
}
