using System.Text.RegularExpressions;

namespace Contracts;

/// <summary>The versioned system-template a business module publishes so a tenant can be given access to it.
/// A module owns its manifest (it may reference only Contracts); Host composes the manifests of every module
/// and hands them to Access, exactly like the action-registry manifest. Access never knows which modules exist.
///
/// Enablement is an explicit, idempotent COPY of the current version into tenant-local rows — there is no
/// reconciler: a tenant enabled at version N is never changed by a later version (Phase 1.5 Decision A).</summary>
public sealed partial record ModuleCapabilityManifest(
    string ModuleKey,
    string Name,
    int Version,
    IReadOnlyList<PermissionSetTemplate> PermissionSets,
    IReadOnlyList<RoleTemplate> Roles)
{
    public const string OwnerRelation = "owner";

    /// <summary>Every action key any permission-set template references, in first-seen order.</summary>
    public IReadOnlyList<string> ActionKeys() =>
        PermissionSets.SelectMany(s => s.Items).Select(i => i.ActionKey).Distinct().ToList();

    /// <summary>Fails fast on a malformed manifest — called when the platform composes its module list, so a
    /// broken template stops start-up instead of surfacing while a tenant is being provisioned.</summary>
    public void Validate()
    {
        if (!ModuleKeyPattern().IsMatch(ModuleKey))
            throw new InvalidOperationException($"Module key '{ModuleKey}' must be lower-case letters, digits or underscores, starting with a letter.");
        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidOperationException($"Module '{ModuleKey}' needs a name.");
        if (Version < 1)
            throw new InvalidOperationException($"Module '{ModuleKey}' version must be at least 1.");
        if (PermissionSets.Count == 0)
            throw new InvalidOperationException($"Module '{ModuleKey}' must ship at least one permission set.");

        RequireUnique(PermissionSets.Select(s => s.Key), $"Module '{ModuleKey}' permission set key");
        RequireUnique(Roles.Select(r => r.Key), $"Module '{ModuleKey}' role key");

        foreach (var set in PermissionSets)
        {
            if (string.IsNullOrWhiteSpace(set.Key) || string.IsNullOrWhiteSpace(set.Name) || set.Items.Count == 0)
                throw new InvalidOperationException($"Permission set '{set.Key}' of module '{ModuleKey}' needs a key, a name and at least one item.");
            RequireUnique(set.Items.Select(i => i.ActionKey), $"Permission set '{set.Key}' action key");
            foreach (var item in set.Items)
            {
                if (string.IsNullOrWhiteSpace(item.ActionKey) || item.ActionKey.Contains('*'))
                    throw new InvalidOperationException($"Permission set '{set.Key}' lists '{item.ActionKey}': explicit action keys only, no wildcards.");
                if (item.Relation is not null && item.Relation != OwnerRelation)
                    throw new InvalidOperationException($"Permission set '{set.Key}' uses unsupported relation '{item.Relation}'.");
            }
        }

        var setKeys = PermissionSets.Select(s => s.Key).ToHashSet();
        foreach (var role in Roles)
        {
            if (string.IsNullOrWhiteSpace(role.Key) || string.IsNullOrWhiteSpace(role.Name) || role.PermissionSetKeys.Count == 0)
                throw new InvalidOperationException($"Role '{role.Key}' of module '{ModuleKey}' needs a key, a name and at least one permission set.");
            RequireUnique(role.PermissionSetKeys, $"Role '{role.Key}' permission set reference");
            var unknown = role.PermissionSetKeys.FirstOrDefault(k => !setKeys.Contains(k));
            if (unknown is not null)
                throw new InvalidOperationException($"Role '{role.Key}' references unknown permission set '{unknown}'.");
        }
    }

    private static void RequireUnique(IEnumerable<string> values, string what)
    {
        var duplicate = values.GroupBy(v => v).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"{what} '{duplicate.Key}' is listed twice.");
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex ModuleKeyPattern();
}
