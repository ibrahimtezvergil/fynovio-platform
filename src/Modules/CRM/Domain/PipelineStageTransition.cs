using Contracts;

namespace CRM.Domain;

/// <summary>A tenant-safe directed edge in a published pipeline version's transition graph.</summary>
public sealed class PipelineStageTransition
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public long PipelineDefinitionVersionId { get; private set; }
    public long FromStageId { get; private set; }
    public long ToStageId { get; private set; }

    private PipelineStageTransition() { }

    public static PipelineStageTransition Create(TenantId tenantId, long versionId, long fromStageId, long toStageId)
    {
        if (fromStageId == toStageId) throw new ArgumentException("A stage cannot transition to itself.", nameof(toStageId));
        return new PipelineStageTransition { TenantId = tenantId, PipelineDefinitionVersionId = versionId, FromStageId = fromStageId, ToStageId = toStageId };
    }
}
