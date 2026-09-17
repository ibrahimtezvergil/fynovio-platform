namespace Contracts;

/// <summary>Read-only check against the platform-owned action registry (round 3
/// ownership matrix: "Action Registry = Platform + owning business domain
/// vocabulary"). Access's PDP denies any unregistered or deprecated key
/// (gap-closure §3) — this is the runtime enforcement, there is no CI-time analyzer
/// in Phase 1.5.</summary>
public interface IActionCatalog
{
    Task<bool> IsActiveAsync(ActionKey action, CancellationToken cancellationToken = default);
}
