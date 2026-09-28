using Contracts;

namespace CRM.Domain;

/// <summary>One row per stage an Opportunity occupied (doc 2026-09-27 §5 item 4) — feeds
/// funnel/time-in-stage reporting. ClosedFromStageId alone (on Opportunity itself) is not
/// enough for full funnel analysis across every stage a deal passed through, only the one
/// right before closing.</summary>
public sealed class OpportunityStageHistoryEntry
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public long OpportunityId { get; private set; }
    public long PipelineDefinitionVersionId { get; private set; }
    public long PipelineStageId { get; private set; }
    public DateTimeOffset EnteredAt { get; private set; }
    public DateTimeOffset? ExitedAt { get; private set; }

    private OpportunityStageHistoryEntry() { }

    public static OpportunityStageHistoryEntry Open(TenantId tenantId, long opportunityId, long pipelineDefinitionVersionId, long pipelineStageId) => new()
    {
        TenantId = tenantId,
        OpportunityId = opportunityId,
        PipelineDefinitionVersionId = pipelineDefinitionVersionId,
        PipelineStageId = pipelineStageId,
        EnteredAt = DateTimeOffset.UtcNow
    };

    public void Close()
    {
        if (ExitedAt is not null) throw new InvalidOperationException("This stage-history entry is already closed.");
        ExitedAt = DateTimeOffset.UtcNow;
    }
}
