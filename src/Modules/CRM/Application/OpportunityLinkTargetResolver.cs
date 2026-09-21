using Contracts;
using CRM.Domain;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application;

/// <summary>Link target `crm/opportunity`: the display data a link from another module (a calendar entry) may show.
/// One tenant-scoped query loads the requested opportunities; each is then authorized with EXACTLY the request
/// GetOpportunityHandler sends (<see cref="OpportunityReadAuthorization"/>: `crm.opportunity.read`, resource =
/// the opportunity with its assigned principal as owner), so the two cannot decide differently. A record that does
/// not exist, belongs to another tenant (RLS + tenant predicate) or is denied is simply absent from the result — the
/// caller maps every absence to <see cref="LinkTargetResolution.Unavailable"/>.
///
/// Label is `#id · party name`; the party name is included only when the actor may also read parties
/// (`crm.reference.party.search`, the gate on that path today) and the directory answers — otherwise `#id`. Parties
/// are looked up only for opportunities the actor may see, so a denied record's party is never touched. The subtitle
/// is the pipeline stage's name. Neither is ever stored by the caller.
///
/// Cost: one CRM round-trip for opportunities (+ one for stages, + one MasterData batch), plus one PDP evaluation per
/// authorized-candidate record — the same decision the read handler makes, not a shortcut around it.</summary>
public sealed class OpportunityLinkTargetResolver(CrmDbContext context, IAuthorizer authorizer, IPartyDirectory partyDirectory) : ILinkTargetResolver
{
    public string BoundedContext => "crm";
    public string EntityType => "opportunity";

    public async Task<IReadOnlyDictionary<long, LinkTargetResolution>> ResolveAsync(
        ActorContext actor, IReadOnlyCollection<long> ids, CancellationToken cancellationToken = default)
    {
        var wanted = ids.Where(id => id > 0).Distinct().ToList();
        var results = new Dictionary<long, LinkTargetResolution>();
        if (wanted.Count == 0)
            return results;

        List<Opportunity> opportunities;
        Dictionary<long, string> stageNames;
        await using (var transaction = await context.Database.BeginTransactionAsync(cancellationToken))
        {
            await context.SetTenantContextAsync(actor.TenantId, cancellationToken);

            opportunities = await context.Opportunities.AsNoTracking()
                .Where(o => o.TenantId == actor.TenantId && wanted.Contains(o.Id))
                .ToListAsync(cancellationToken);

            var stageIds = opportunities.Where(o => o.PipelineStageId is not null).Select(o => o.PipelineStageId!.Value).Distinct().ToList();
            stageNames = stageIds.Count == 0
                ? []
                : await context.PipelineStages.AsNoTracking()
                    .Where(s => s.TenantId == actor.TenantId && stageIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }

        var allowed = new List<Opportunity>();
        foreach (var opportunity in opportunities)
            if (await OpportunityReadAuthorization.IsAllowedAsync(authorizer, actor, opportunity, cancellationToken))
                allowed.Add(opportunity);

        var partyNames = allowed.Count == 0
            ? []
            : await ResolvePartyNamesAsync(actor, allowed.Select(o => o.PartyRefPartyId).Distinct().ToList(), cancellationToken);

        foreach (var opportunity in allowed)
        {
            var label = partyNames.TryGetValue(opportunity.PartyRefPartyId, out var partyName)
                ? $"#{opportunity.Id} · {partyName}"
                : $"#{opportunity.Id}";
            var subtitle = opportunity.PipelineStageId is { } stageId && stageNames.TryGetValue(stageId, out var stageName) ? stageName : null;
            results[opportunity.Id] = new LinkTargetResolution.Accessible(label, subtitle);
        }

        return results;
    }

    /// <summary>Party names are decoration: a denied party gate or a MasterData failure degrades the label to `#id`
    /// rather than failing (or leaking) the opportunity link. The gate is the same action and resource the party
    /// lookup itself uses (<see cref="SearchPartyReferencesHandler"/>).</summary>
    private async Task<Dictionary<long, string>> ResolvePartyNamesAsync(
        ActorContext actor, IReadOnlyCollection<long> partyIds, CancellationToken cancellationToken)
    {
        try
        {
            var decision = await authorizer.AuthorizeAsync(
                new AuthorizationRequest(actor, new ActionKey(CrmActionKeys.PartyReferenceSearch), new ResourceDescriptor("PartyReference", null, null)),
                cancellationToken);
            if (!decision.IsAllowed)
                return [];

            var refs = partyIds.Select(id => new PartyRef(actor.TenantId, id)).ToList();
            var found = await partyDirectory.GetPartiesAsync(refs, cancellationToken);
            return refs.Where(found.ContainsKey).ToDictionary(r => r.PartyId, r => PartyDisplay.Name(found[r]));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return [];
        }
    }
}
