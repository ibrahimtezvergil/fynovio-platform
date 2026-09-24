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
    public bool IsActive { get; private set; } = true;
    public bool IsArchived { get; private set; }
    public long RowVersion { get; private set; } = 1;
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

    /// <summary>Stamps the version with this definition's current `Id`. `Versions` is
    /// EF-`Ignore()`d, so callers must first save the definition or allocate its identity
    /// from the database sequence; there is no navigation-based FK fixup.</summary>
    public PipelineDefinitionVersion AddVersion(int versionNumber)
    {
        if (IsArchived)
            throw new InvalidOperationException("An archived pipeline cannot receive a new version.");
        if (_versions.Any(v => v.VersionNumber == versionNumber))
            throw new InvalidOperationException($"Version {versionNumber} already exists on this definition.");

        var version = PipelineDefinitionVersion.Create(TenantId, Id, versionNumber);
        _versions.Add(version);
        UpdatedAt = DateTimeOffset.UtcNow;
        RowVersion++;
        return version;
    }

    public void Rename(string name)
    {
        if (IsArchived) throw new InvalidOperationException("An archived pipeline cannot be edited.");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) throw new ArgumentException("A pipeline name of 1–100 characters is required.", nameof(name));
        Name = name.Trim();
        Touch();
    }

    public void SetActive(bool active)
    {
        if (IsArchived && active) throw new InvalidOperationException("An archived pipeline cannot be activated.");
        IsActive = active;
        Touch();
    }

    public void Archive()
    {
        IsArchived = true;
        IsActive = false;
        Touch();
    }

    public void Restore()
    {
        if (!IsArchived) throw new InvalidOperationException("Only an archived pipeline can be restored.");
        IsArchived = false;
        IsActive = false;
        Touch();
    }

    public void MarkConfigurationChanged() => Touch();

    private void Touch()
    {
        RowVersion++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
