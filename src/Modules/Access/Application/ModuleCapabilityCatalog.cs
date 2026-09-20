using Contracts;

namespace Access.Application;

/// <summary>The manifests of every module the platform can enable for a tenant, composed by Host (Access never
/// references a business module). Validated once at construction so a malformed template stops start-up.</summary>
public sealed class ModuleCapabilityCatalog
{
    private readonly Dictionary<string, ModuleCapabilityManifest> _byKey;

    public ModuleCapabilityCatalog(IEnumerable<ModuleCapabilityManifest> manifests)
    {
        var list = manifests.ToList();
        foreach (var manifest in list)
            manifest.Validate();

        var duplicate = list.GroupBy(m => m.ModuleKey).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"Module '{duplicate.Key}' is registered twice.");

        _byKey = list.ToDictionary(m => m.ModuleKey);
        Modules = list;
    }

    public IReadOnlyList<ModuleCapabilityManifest> Modules { get; }

    public ModuleCapabilityManifest? Find(string moduleKey) => _byKey.GetValueOrDefault(moduleKey);
}
