using Contracts;

namespace CRM.Application;

/// <summary>The reference query behind the opportunity form's customer picker. CRM does not own Parties — MasterData
/// does — so this only authorizes the CRM user (`crm.reference.party.search`, no resource) and asks the Party
/// directory through Contracts; it neither copies nor caches party data. MasterData has no authorization layer of
/// its own yet, so this CRM action is the gate on that read path (Phase 2.6 plan, limitation L-3).</summary>
public sealed class SearchPartyReferencesHandler(IAuthorizer authorizer, IPartySearch search, IPartyDirectory directory)
{
    private const string ActionKeyValue = CrmActionKeys.PartyReferenceSearch;
    public const int DefaultTake = 20;
    public const int MaxIds = 50;

    public async Task<IReadOnlyList<PartyReferenceDto>> HandleAsync(SearchPartyReferencesQuery query, CancellationToken cancellationToken = default)
    {
        var actor = new ActorContext(query.TenantId, query.Principal, query.CorrelationId);
        var decision = await authorizer.AuthorizeAsync(
            new AuthorizationRequest(actor, new ActionKey(ActionKeyValue), new ResourceDescriptor("PartyReference", null, null)), cancellationToken);
        if (!decision.IsAllowed)
            throw new OpportunityAuthorizationDeniedException(ActionKeyValue, decision.ReasonCode, decision.DenialStage);

        if (query.Ids is { Count: > 0 } ids)
            return await LookUpAsync(query.TenantId, ids, cancellationToken);

        var found = await search.SearchPartiesAsync(query.TenantId, query.Search, query.Take <= 0 ? DefaultTake : query.Take, cancellationToken);
        return found.Select(ToDto).ToList();
    }

    private async Task<IReadOnlyList<PartyReferenceDto>> LookUpAsync(TenantId tenantId, IReadOnlyCollection<long> ids, CancellationToken cancellationToken)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count > MaxIds || distinct.Any(id => id <= 0))
            throw new ArgumentException($"ids must be at most {MaxIds} positive identifiers.");

        var refs = distinct.Select(id => new PartyRef(tenantId, id)).ToList();
        var resolved = await directory.GetPartiesAsync(refs, cancellationToken);
        return refs.Where(resolved.ContainsKey).Select(r => ToDto(resolved[r])).ToList();
    }

    private static PartyReferenceDto ToDto(PartyDirectoryEntry entry) =>
        new(entry.PartyRef.PartyId, entry.PartyType.ToString(), PartyDisplay.Name(entry), entry.Email);
}
