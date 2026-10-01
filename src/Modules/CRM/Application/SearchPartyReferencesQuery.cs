using Contracts;

namespace CRM.Application;

/// <summary>Either `Ids` (resolve display info for parties the UI already holds ids for — e.g. an opportunity's
/// customer) or `Search` (type-ahead for picking one). `Ids` wins when both are given.</summary>
public sealed record SearchPartyReferencesQuery(
    TenantId TenantId,
    PrincipalRef Principal,
    string? Search,
    IReadOnlyCollection<long>? Ids,
    int Take,
    Guid CorrelationId);

/// <summary>The display fields of a Party (including the phone the customer picker shows), nothing else — no external identities, no merge internals.</summary>
public sealed record PartyReferenceDto(long Id, string PartyType, string DisplayName, string? Email, string? Phone = null);
