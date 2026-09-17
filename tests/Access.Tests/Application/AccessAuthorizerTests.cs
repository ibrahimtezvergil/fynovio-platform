using Access.Application;
using Contracts;

namespace Access.Tests.Application;

public sealed class AccessAuthorizerTests : IClassFixture<AccessTestFixture>
{
    private readonly AccessTestFixture _fixture;

    public AccessAuthorizerTests(AccessTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Unregistered_action_is_denied()
    {
        await using var context = _fixture.CreateContext();
        var actionCatalog = new AccessActionCatalogService(context);
        var authorizer = new AccessAuthorizer(context, new PrincipalResolver(context), actionCatalog);

        var actor = new ActorContext(new TenantId(1), new PrincipalRef("test-idp", "nobody"), Guid.NewGuid());
        var decision = await authorizer.AuthorizeAsync(new AuthorizationRequest(
            actor, new ActionKey("never.registered.action"), new ResourceDescriptor("Test.Resource", null, null)));

        Assert.False(decision.IsAllowed);
        Assert.Equal("action_not_registered", decision.ReasonCode);
    }

    [Fact]
    public async Task Unrecognized_principal_is_denied()
    {
        var (authorizer, _, actor, action) = await _fixture.GivenOwnerRelationGrantAsync();
        var strangerPrincipal = new PrincipalRef("test-idp", $"stranger-{Guid.NewGuid():N}");
        var strangerActor = new ActorContext(actor.TenantId, strangerPrincipal, actor.CorrelationId);

        var decision = await authorizer.AuthorizeAsync(new AuthorizationRequest(
            strangerActor, action, new ResourceDescriptor("Test.Resource", 1, strangerPrincipal)));

        Assert.False(decision.IsAllowed);
        Assert.Equal("principal_not_recognized", decision.ReasonCode);
    }

    [Fact]
    public async Task Owner_relation_grant_denies_a_non_owned_resource()
    {
        var (authorizer, _, actor, action) = await _fixture.GivenOwnerRelationGrantAsync();
        var resource = new ResourceDescriptor("Test.Resource", 1, new PrincipalRef("test-idp", "someone-else"));

        var decision = await authorizer.AuthorizeAsync(new AuthorizationRequest(actor, action, resource));

        Assert.False(decision.IsAllowed);
        Assert.Equal("no_matching_grant", decision.ReasonCode);
    }

    [Fact]
    public async Task Unrestricted_grant_allows_regardless_of_resource_owner()
    {
        var tenantId = new TenantId(200);
        var principal = new PrincipalRef("test-idp", $"unrestricted-actor-{Guid.NewGuid():N}");
        var (authorizer, _, actor, action) = await _fixture.GivenGrantAsync(
            tenantId, principal, "test.resource.read", "Test", "Test.Resource", relation: null);

        var resource = new ResourceDescriptor("Test.Resource", 1, new PrincipalRef("test-idp", "someone-else"));
        var decision = await authorizer.AuthorizeAsync(new AuthorizationRequest(actor, action, resource));

        Assert.True(decision.IsAllowed);
        Assert.Equal("tenant_scope_grant", decision.ReasonCode);
    }
}
