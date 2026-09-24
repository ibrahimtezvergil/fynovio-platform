namespace Contracts;

/// <summary>"Which members of this tenant could hold a resource that requires these actions?" — answered by
/// Access from the same grants its PDP evaluates, so a business module never filters candidates itself and
/// never sees a member who could not act on the record. Deliberately NOT a generic tenant user directory:
/// every result is a member who is ACTIVE and permitted EVERY required action, evaluated as if they owned the
/// resource (an `owner`-relation grant counts; it would apply to them once assigned).
///
/// Today the PDP evaluates RBAC + tenant scope + the `owner` relation (Phase 1.5); team, org and territory
/// fact providers do not exist. When the PDP learns them, this contract is where they are consumed — callers
/// and the frontend do not change. Fails closed: an unregistered/deprecated action yields nobody.</summary>
public interface IAuthorizedPrincipalDirectory
{
    /// <summary>Resolve names for principals already present on tenant-scoped resources.</summary>
    Task<IReadOnlyDictionary<PrincipalRef, string>> ResolveDisplayNamesAsync(
        TenantId tenantId, IReadOnlyCollection<PrincipalRef> principals, CancellationToken cancellationToken = default);

    /// <summary>Active members permitted every action in <paramref name="requiredActions"/>, ordered by display
    /// name. `search` matches display name or e-mail (case-insensitive, literal). `take` is clamped to 1..50.</summary>
    Task<IReadOnlyList<PrincipalDirectoryEntry>> ListPermittedPrincipalsAsync(
        TenantId tenantId,
        IReadOnlyCollection<ActionKey> requiredActions,
        string? search,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>The same predicate for one principal — the server-side re-check a command performs on a target the
    /// client chose. True exactly when the principal would appear in <see cref="ListPermittedPrincipalsAsync"/>.</summary>
    Task<bool> IsPrincipalPermittedAsync(
        TenantId tenantId,
        PrincipalRef principal,
        IReadOnlyCollection<ActionKey> requiredActions,
        CancellationToken cancellationToken = default);
}
