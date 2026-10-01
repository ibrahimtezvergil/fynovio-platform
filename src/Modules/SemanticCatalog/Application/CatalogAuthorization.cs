using Contracts;
using SemanticCatalog.Domain;

namespace SemanticCatalog.Application;

/// <summary>The catalog cannot call the owner module's handlers, so it evaluates the owner's settings actions itself,
/// with the same action keys and the same resource descriptor (`CrmSettings`) the CRM handlers use
/// (adr-semantic-catalog-changeset.md S-4). `CatalogAuthorizationAgreementTests` proves the two cannot drift.</summary>
internal static class CatalogAuthorization
{
    public static async Task AuthorizeAsync(
        TenantId tenantId, PrincipalRef principal, Guid correlationId, string ownerContext, bool write, IAuthorizer authorizer, CancellationToken cancellationToken)
    {
        var owner = CatalogOwners.Require(ownerContext);
        var actionKey = write ? owner.WriteAction : owner.ReadAction;
        var decision = await authorizer.AuthorizeAsync(new AuthorizationRequest(
            new ActorContext(tenantId, principal, correlationId), new ActionKey(actionKey), new ResourceDescriptor(owner.ResourceType, null, null)), cancellationToken);
        if (!decision.IsAllowed)
            throw new CatalogAuthorizationDeniedException(actionKey, decision.ReasonCode, decision.DenialStage);
    }
}
