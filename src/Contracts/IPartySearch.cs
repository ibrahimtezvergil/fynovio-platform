namespace Contracts;

/// <summary>Type-ahead lookup over a tenant's Parties, for a consumer that lets a user PICK one (a CRM opportunity's
/// customer). Read/display-oriented like <see cref="IPartyDirectory"/> — never a command precondition (use
/// <see cref="IPartyIdentityResolver"/> for that). Tenant-scoped by construction: the tenant is a parameter and the
/// implementation sets the database tenant context, so under RLS nothing from another tenant can appear.
/// Merged (tombstone) parties never appear — only their surviving party does.</summary>
public interface IPartySearch
{
    /// <summary>Parties whose name, surname, full name, e-mail or phone contains `query` (case-insensitive, literal — `%` and `_`
    /// are not wildcards; a phone matches on its digits, so `0532 111` finds `+90 532 111 22 33`), or whose id equals a numeric `query`
    /// (the customer number), ordered by name. A blank query lists the first parties alphabetically. `take` is clamped to 1..50.</summary>
    Task<IReadOnlyList<PartyDirectoryEntry>> SearchPartiesAsync(
        TenantId tenantId, string? query, int take, CancellationToken cancellationToken = default);
}
