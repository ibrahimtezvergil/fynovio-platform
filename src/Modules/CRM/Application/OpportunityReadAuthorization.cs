using Contracts;
using CRM.Domain;

namespace CRM.Application;

/// <summary>The one place the single-record `crm.opportunity.read` authorization request is built. Every reader that
/// shows an individual Opportunity (the read handler, link resolution) asks through here, so the action key and the
/// resource descriptor — including the owner fact the PDP evaluates — cannot drift apart between them.</summary>
internal static class OpportunityReadAuthorization
{
    public const string ActionKeyValue = CrmActionKeys.OpportunityRead;

    public static AuthorizationRequest Request(ActorContext actor, Opportunity opportunity) =>
        new(actor, new ActionKey(ActionKeyValue), new ResourceDescriptor(nameof(Opportunity), opportunity.Id, opportunity.AssignedPrincipal));

    public static async Task<bool> IsAllowedAsync(
        IAuthorizer authorizer, ActorContext actor, Opportunity opportunity, CancellationToken cancellationToken) =>
        (await authorizer.AuthorizeAsync(Request(actor, opportunity), cancellationToken)).IsAllowed;
}
