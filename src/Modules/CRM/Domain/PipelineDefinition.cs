using Contracts;

namespace CRM.Domain;

/// <summary>Tenant/sector-configurable pipeline, versioned (PDF §5) so an edit to an
/// in-use definition never silently remaps opportunities already on an older version —
/// see PipelineDefinitionVersion. Purely additive in Phase 1: no command assigns one to
/// an Opportunity yet (Phase 2's ChangePipelineStage does).</summary>
public sealed class PipelineDefinition
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<PipelineDefinitionVersion> _versions = [];
    public IReadOnlyCollection<PipelineDefinitionVersion> Versions => _versions;

    private PipelineDefinition() { }

    public static PipelineDefinition Create(TenantId tenantId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        var now = DateTimeOffset.UtcNow;
        return new PipelineDefinition
        {
            TenantId = tenantId,
            Name = name,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public PipelineDefinitionVersion AddVersion(int versionNumber)
    {
        if (_versions.Any(v => v.VersionNumber == versionNumber))
            throw new InvalidOperationException($"Version {versionNumber} already exists on this definition.");

        var version = PipelineDefinitionVersion.Create(TenantId, versionNumber);
        _versions.Add(version);
        UpdatedAt = DateTimeOffset.UtcNow;
        return version;
    }
}
