using Contracts;

namespace CRM.Application;

/// <summary>Link target `masterdata/party`, implemented in CRM as an INTERIM under AGENTS.md limitation L-3:
/// MasterData has no authorization layer of its own, and `crm.reference.party.search` is the gate on party reads
/// today (see <see cref="SearchPartyReferencesHandler"/>, whose lookup-by-ids path this mirrors exactly: same action,
/// same directory). When MasterData gains its own PDP this resolver moves there and CRM stops answering for it.
/// A merged party is shown as its survivor (the directory resolves the tombstone); a missing party, another tenant's
/// party, a merged party without a survivor and a denied actor are all <see cref="LinkTargetResolution.Unavailable"/>.
/// Label is the party's display name; there is deliberately no subtitle (no server-side, untranslatable text).</summary>
public sealed class PartyLinkTargetResolver(IAuthorizer authorizer, IPartyDirectory partyDirectory) : ILinkTargetResolver
{
    public string BoundedContext => "masterdata";
    public string EntityType => "party";

    public async Task<IReadOnlyDictionary<long, LinkTargetResolution>> ResolveAsync(
        ActorContext actor, IReadOnlyCollection<long> ids, CancellationToken cancellationToken = default)
    {
        var wanted = ids.Where(id => id > 0).Distinct().ToList();
        var results = new Dictionary<long, LinkTargetResolution>();
        if (wanted.Count == 0)
            return results;

        var decision = await authorizer.AuthorizeAsync(
            new AuthorizationRequest(actor, new ActionKey(CrmActionKeys.PartyReferenceSearch), new ResourceDescriptor("PartyReference", null, null)),
            cancellationToken);
        if (!decision.IsAllowed)
            return results;

        var refs = wanted.Select(id => new PartyRef(actor.TenantId, id)).ToList();
        var found = await partyDirectory.GetPartiesAsync(refs, cancellationToken);
        foreach (var reference in refs.Where(found.ContainsKey))
            results[reference.PartyId] = new LinkTargetResolution.Accessible(PartyDisplay.Name(found[reference]));

        return results;
    }
}
