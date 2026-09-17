using Contracts;

namespace Access.Tests.Application;

/// <summary>Guards the round 3 §5 invariant: Authorize(actor, action, row) == Allow
/// IFF row ∈ ResolveAccessScope(actor, action, resourceType). Uses a synthetic
/// resource — no CRM/domain adapter exists yet (that's Phase 2); this proves the
/// Access-side contract is internally consistent before any consumer depends on it.</summary>
public sealed class AuthorizeResolveAccessScopeEquivalenceTests : IClassFixture<AccessTestFixture>
{
    private readonly AccessTestFixture _fixture;

    public AuthorizeResolveAccessScopeEquivalenceTests(AccessTestFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData(true)]  // resource owned by the actor
    [InlineData(false)] // resource owned by someone else
    public async Task Owner_relation_grant_is_consistent_across_both_contracts(bool actorOwnsResource)
    {
        var (authorizer, scopeResolver, actor, action) = await _fixture.GivenOwnerRelationGrantAsync();
        var ownerPrincipal = actorOwnsResource ? actor.Principal : new PrincipalRef("test-idp", "someone-else");
        var resource = new ResourceDescriptor("Test.Resource", 1, ownerPrincipal);

        var decision = await authorizer.AuthorizeAsync(new AuthorizationRequest(actor, action, resource));
        var scope = await scopeResolver.ResolveAsync(actor, action, resource.ResourceType);

        var includedByScope = scope switch
        {
            AccessScope.None => false,
            AccessScope.All => true,
            AccessScope.AnyOf anyOf => anyOf.Terms.OfType<ScopeTerm.OwnedBy>().Any(t => t.Principal == ownerPrincipal),
            _ => false
        };

        Assert.Equal(decision.IsAllowed, includedByScope);
    }
}
