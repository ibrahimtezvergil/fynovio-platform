using Access.Application.Authentication;
using Access.Domain.Identity;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Application;

/// <summary>Implements <see cref="IAuthorizedPrincipalDirectory"/> over the tables the PDP reads (active
/// role assignment → role → permission set → item). The rule is the PDP's, restated set-based:
/// an assignment counts when it is valid now; an item counts whatever its relation, because the candidate is
/// the prospective owner. `AuthorizedPrincipalDirectoryTests` proves the two agree for every member across a
/// grant matrix — if the PDP gains a rule (teams, territories) that test fails until this follows.
///
/// Candidates are members of the tenant with an ACTIVE membership, identified by their platform-issuer
/// identity — the pair a signed-in user's JWT carries, and therefore the pair `OwnedBy` compares.</summary>
public sealed class AuthorizedPrincipalDirectory(
    AccessDbContext context,
    IActionCatalog actionCatalog,
    SessionOptions sessionOptions) : IAuthorizedPrincipalDirectory
{
    public const int MaxTake = 50;
    private const int MaxSearchLength = 100;

    public Task<IReadOnlyDictionary<PrincipalRef, string>> ResolveDisplayNamesAsync(
        TenantId tenantId, IReadOnlyCollection<PrincipalRef> principals, CancellationToken cancellationToken = default) =>
        InTenantAsync<IReadOnlyDictionary<PrincipalRef, string>>(tenantId, async () =>
        {
            if (principals.Count == 0) return new Dictionary<PrincipalRef, string>();
            var issuers = principals.Select(p => p.Issuer).Distinct().ToArray();
            var subjects = principals.Select(p => p.Subject).Distinct().ToArray();
            var rows = await (from membership in context.TenantMemberships
                              join identity in context.ExternalIdentities on membership.AccountId equals identity.AccountId
                              join account in context.Accounts on membership.AccountId equals account.Id
                              where membership.TenantId == tenantId
                                    && issuers.Contains(identity.Issuer) && subjects.Contains(identity.Subject)
                              select new { identity.Issuer, identity.Subject, account.DisplayName })
                .ToListAsync(cancellationToken);
            var requested = principals.ToHashSet();
            return rows.Where(x => requested.Contains(new PrincipalRef(x.Issuer, x.Subject)))
                .ToDictionary(x => new PrincipalRef(x.Issuer, x.Subject), x => x.DisplayName);
        }, cancellationToken);

    public Task<IReadOnlyList<PrincipalDirectoryEntry>> ListPermittedPrincipalsAsync(
        TenantId tenantId, IReadOnlyCollection<ActionKey> requiredActions, string? search, int take, CancellationToken cancellationToken = default) =>
        InTenantAsync<IReadOnlyList<PrincipalDirectoryEntry>>(tenantId, async () =>
        {
            var candidates = await PermittedCandidatesAsync(tenantId, requiredActions, cancellationToken);
            if (candidates is null)
                return [];

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = "%" + EscapeLike(search.Trim()) + "%";
                candidates = candidates.Where(c => EF.Functions.ILike(c.DisplayName, pattern, "\\") || EF.Functions.ILike(c.Email, pattern, "\\"));
            }

            var rows = await candidates
                .OrderBy(c => c.DisplayName).ThenBy(c => c.Subject)
                .Take(Math.Clamp(take, 1, MaxTake))
                .ToListAsync(cancellationToken);
            return rows.Select(c => new PrincipalDirectoryEntry(new PrincipalRef(c.Issuer, c.Subject), c.DisplayName, c.Email)).ToList();
        }, cancellationToken);

    public Task<bool> IsPrincipalPermittedAsync(
        TenantId tenantId, PrincipalRef principal, IReadOnlyCollection<ActionKey> requiredActions, CancellationToken cancellationToken = default) =>
        InTenantAsync(tenantId, async () =>
        {
            var candidates = await PermittedCandidatesAsync(tenantId, requiredActions, cancellationToken);
            return candidates is not null
                && await candidates.AnyAsync(c => c.Issuer == principal.Issuer && c.Subject == principal.Subject, cancellationToken);
        }, cancellationToken);

    /// <summary>Null when nobody can qualify (an action is unregistered/deprecated) — fail closed.</summary>
    private async Task<IQueryable<Candidate>?> PermittedCandidatesAsync(
        TenantId tenantId, IReadOnlyCollection<ActionKey> requiredActions, CancellationToken cancellationToken)
    {
        if (requiredActions.Count == 0)
            throw new ArgumentException("At least one required action is needed.", nameof(requiredActions));
        if (string.IsNullOrWhiteSpace(sessionOptions.PlatformIssuer))
            throw new InvalidOperationException("The platform issuer is not configured.");

        foreach (var action in requiredActions)
        {
            if (!await actionCatalog.IsActiveAsync(action, cancellationToken))
                return null;
        }

        var issuer = sessionOptions.PlatformIssuer;
        var now = DateTimeOffset.UtcNow;

        var members = context.TenantMemberships.Where(m => m.TenantId == tenantId && m.Status == MembershipStatus.Active);
        foreach (var action in requiredActions.Select(a => a.Value).Distinct())
        {
            var permitted = PermittedAccounts(tenantId, action, now);
            members = members.Where(m => permitted.Contains(m.AccountId));
        }

        return
            from membership in members
            join identity in context.ExternalIdentities on membership.AccountId equals identity.AccountId
            join account in context.Accounts on membership.AccountId equals account.Id
            where identity.Issuer == issuer
            select new Candidate
            {
                AccountId = membership.AccountId,
                Issuer = identity.Issuer,
                Subject = identity.Subject,
                DisplayName = account.DisplayName,
                Email = account.Email
            };
    }

    private IQueryable<long> PermittedAccounts(TenantId tenantId, string action, DateTimeOffset now) =>
        from assignment in context.RoleAssignments
        join link in context.RolePermissionSets on assignment.RoleId equals link.RoleId
        join item in context.PermissionSetItems on link.PermissionSetId equals item.PermissionSetId
        where assignment.TenantId == tenantId && link.TenantId == tenantId && item.TenantId == tenantId
            && assignment.ValidFrom <= now && (assignment.ValidTo == null || now < assignment.ValidTo)
            && item.ActionKey == action
        select assignment.AccountId;

    /// <summary>Reentrant like the PDP: a caller that already holds a transaction on this context with the tenant
    /// set must not have a second one opened underneath it.</summary>
    private async Task<T> InTenantAsync<T>(TenantId tenantId, Func<Task<T>> work, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is not null)
            return await work();

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(tenantId, cancellationToken);
        var result = await work();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static string EscapeLike(string value)
    {
        var trimmed = value.Length > MaxSearchLength ? value[..MaxSearchLength] : value;
        return trimmed.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    }

    // A class with member-init (not a positional record): EF translates OrderBy/Where on it, but not on a
    // constructor-bound projection.
    private sealed class Candidate
    {
        public long AccountId { get; init; }
        public string Issuer { get; init; } = null!;
        public string Subject { get; init; } = null!;
        public string DisplayName { get; init; } = null!;
        public string Email { get; init; } = null!;
    }
}
