using Contracts;

namespace CRM.Domain;

public sealed class PipelineStage
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public long PipelineDefinitionVersionId { get; private set; }
    public string Name { get; private set; } = null!;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsEntry { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private PipelineStage() { }

    internal static PipelineStage Create(TenantId tenantId, long pipelineDefinitionVersionId, string name, int sortOrder, bool isEntry)
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
            IsActive = true,
            IsEntry = isEntry,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>Retired stages remain valid FK targets for historical Opportunities
    /// (Persistence/Configurations/OpportunityConfiguration.cs uses
    /// DeleteBehavior.Restrict) — this only excludes the stage from
    /// ChangePipelineStage's future valid-target list (architecture plan §2.4).</summary>
    public void Deactivate() => IsActive = false;

    internal void SetEntry(bool isEntry) => IsEntry = isEntry;
}
