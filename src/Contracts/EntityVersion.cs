namespace Contracts;

/// <summary>Optimistic concurrency token scoped to one <see cref="EntityRef"/>. Each version is immutable;
/// callers pass the expected version back on mutation and the owner rejects a stale one.</summary>
public readonly record struct EntityVersion
{
    public EntityRef Entity { get; }
    public long Version { get; }

    public EntityVersion(EntityRef entity, long version)
    {
        if (version < 0)
            throw new ArgumentOutOfRangeException(nameof(version), "Version cannot be negative.");

        Entity = entity;
        Version = version;
    }

    public override string ToString() => $"{Entity}@v{Version}";
}
