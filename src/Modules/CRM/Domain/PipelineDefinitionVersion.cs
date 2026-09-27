using Contracts;

namespace CRM.Domain;

public enum PipelineVersionStatus
{
    Draft,
    Published,
    Superseded,
    Archived
}

public sealed class PipelineDefinitionVersion
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public long PipelineDefinitionId { get; private set; }
    public int VersionNumber { get; private set; }
    public PipelineVersionStatus Status { get; private set; } = PipelineVersionStatus.Draft;
    public bool EnforceAllowedTransitions { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
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

    /// <summary>Stamps the stage with this version's current `Id` — the version must be
    /// saved or have its identity allocated before this call (`Stages` is EF-`Ignore()`d).
    /// The first stage added to a version
    /// becomes its entry stage by default (architecture plan §2.3 OD#2, option (i)) —
    /// callers that want a different entry stage call `MarkEntry` afterward.</summary>
    public PipelineStage AddStage(string name, int sortOrder)
    {
        if (Status != PipelineVersionStatus.Draft) throw new InvalidOperationException("Only a draft pipeline version can be edited.");
        if (_stages.Any(s => s.SortOrder == sortOrder))
            throw new InvalidOperationException($"Sort order {sortOrder} is already used on this version.");

        var stage = PipelineStage.Create(TenantId, Id, name, sortOrder, isEntry: _stages.Count == 0);
        _stages.Add(stage);
        return stage;
    }

    public PipelineStage AddWonStage(string name, int sortOrder) => AddSystemStage(PipelineStageKind.Won, name, sortOrder);

    public PipelineStage AddLostStage(string name, int sortOrder) => AddSystemStage(PipelineStageKind.Lost, name, sortOrder);

    private PipelineStage AddSystemStage(PipelineStageKind kind, string name, int sortOrder)
    {
        if (Status != PipelineVersionStatus.Draft) throw new InvalidOperationException("Only a draft pipeline version can be edited.");
        if (_stages.Any(s => s.Kind == kind)) throw new InvalidOperationException($"This version already has a {kind} stage.");
        if (_stages.Any(s => s.SortOrder == sortOrder)) throw new InvalidOperationException($"Sort order {sortOrder} is already used on this version.");

        var stage = PipelineStage.Create(TenantId, Id, name, sortOrder, isEntry: false, kind);
        _stages.Add(stage);
        return stage;
    }

    /// <summary>One-time migration exception
    /// (docs/architecture-analysis/2026-09-27-crm-opportunity-pipeline-won-lost-stage-integration.md
    /// §5 item 6.a) — adds a system Won/Lost stage to an ALREADY Published or Superseded
    /// version, which every other mutation on this aggregate forbids. Never call this from
    /// normal application code; it exists solely for the one-time backfill operator command
    /// (Host.Bootstrap.BackfillCrmPipelinesCommand).</summary>
    public PipelineStage BackfillSystemStage(PipelineStageKind kind, string name, int sortOrder)
    {
        if (kind == PipelineStageKind.Open) throw new ArgumentException("Only Won/Lost system stages can be backfilled.", nameof(kind));
        if (Status is not (PipelineVersionStatus.Published or PipelineVersionStatus.Superseded))
            throw new InvalidOperationException("Backfill only targets a published or superseded version.");
        if (_stages.Any(s => s.Kind == kind)) throw new InvalidOperationException($"This version already has a {kind} stage.");
        if (_stages.Any(s => s.SortOrder == sortOrder)) throw new InvalidOperationException($"Sort order {sortOrder} is already used on this version.");

        var stage = PipelineStage.Create(TenantId, Id, name, sortOrder, isEntry: false, kind);
        _stages.Add(stage);
        return stage;
    }

    /// <summary>Enforces "exactly one entry stage per version" (architecture plan §2.3) —
    /// a cross-row invariant within this aggregate's own child collection, same pattern
    /// as Opportunity.Win()'s billable-line invariant.
    ///
    /// PERSISTENCE WARNING: the partial unique index `ux_pipeline_stages_one_entry_per_version`
    /// is a plain Postgres index, not a deferrable constraint (Postgres does not support
    /// deferrable *partial* unique constraints), so it is checked immediately after each
    /// UPDATE statement, not at transaction commit. A single `SaveChangesAsync()` call that
    /// both unsets the old entry stage and sets the new one is only safe if EF Core happens
    /// to emit the "unset" statement before the "set" statement — which is NOT guaranteed for
    /// two same-type sibling entities with no FK relationship between them. Callers that
    /// persist the effect of `MarkEntry` MUST do it in two steps to be safe regardless of
    /// internal EF ordering: (1) call `MarkEntry`, then `SaveChangesAsync()` while suppressing
    /// or deferring the new stage's `true` write, or more simply, (2) unset the previous entry
    /// stage and save first, then set the new one and save second — never rely on a single
    /// `SaveChangesAsync()` to apply both sides atomically. See
    /// `PipelineConstraintTests.MarkEntry_persisted_*` for the proven-safe two-phase pattern.</summary>
    public void MarkEntry(PipelineStage stage)
    {
        if (Status != PipelineVersionStatus.Draft) throw new InvalidOperationException("Only a draft pipeline version can be edited.");
        if (!_stages.Contains(stage))
            throw new InvalidOperationException("Stage does not belong to this pipeline definition version.");

        foreach (var s in _stages)
            s.SetEntry(ReferenceEquals(s, stage));
    }

    public void SetTransitionMode(bool enforceAllowedTransitions)
    {
        if (Status != PipelineVersionStatus.Draft) throw new InvalidOperationException("Only a draft pipeline version can be edited.");
        EnforceAllowedTransitions = enforceAllowedTransitions;
    }

    public void Publish()
    {
        if (Status != PipelineVersionStatus.Draft) throw new InvalidOperationException("Only a draft pipeline version can be published.");
        Status = PipelineVersionStatus.Published;
        PublishedAt = DateTimeOffset.UtcNow;
    }

    public void Supersede()
    {
        if (Status != PipelineVersionStatus.Published) throw new InvalidOperationException("Only a published version can be superseded.");
        Status = PipelineVersionStatus.Superseded;
    }

    public void ArchiveDraft()
    {
        if (Status != PipelineVersionStatus.Draft) throw new InvalidOperationException("Only a draft version can be archived.");
        Status = PipelineVersionStatus.Archived;
    }
}
