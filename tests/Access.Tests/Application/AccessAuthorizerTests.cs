using Access.Application;
using Access.Domain.Authorization;
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
        Assert.Equal(AuthorizationDenialStage.Coarse, decision.DenialStage);
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
        Assert.Equal(AuthorizationDenialStage.Coarse, decision.DenialStage);
    }

    /// <summary>The `AuthorizationDenialStage.Record` case: the actor holds a real grant
    /// for this action (an owner-relation one), it just doesn't cover this resource — a
    /// PEP may externally collapse this into the same shape as "not found," never `403`
    /// (2026-09-19 authorization-delta doc). Distinguishes this from a `Coarse` denial
    /// (no grant at all), where that collapse must never happen.</summary>
    [Fact]
    public async Task Owner_relation_grant_denies_a_non_owned_resource()
    {
        var (authorizer, _, actor, action) = await _fixture.GivenOwnerRelationGrantAsync();
        var resource = new ResourceDescriptor("Test.Resource", 1, new PrincipalRef("test-idp", "someone-else"));

        var decision = await authorizer.AuthorizeAsync(new AuthorizationRequest(actor, action, resource));

        Assert.False(decision.IsAllowed);
        Assert.Equal("no_matching_grant", decision.ReasonCode);
        Assert.Equal(AuthorizationDenialStage.Record, decision.DenialStage);
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
        Assert.Equal(AuthorizationDenialStage.None, decision.DenialStage);
    }

    /// <summary>C1: a recognized principal, a registered action, but zero effective
    /// grants (no matching RoleAssignment/PermissionSetItem at all) must resolve to
    /// `AccessScope.None` — distinct from an owner-relation grant that merely excludes
    /// a specific resource (covered by Owner_relation_grant_denies_a_non_owned_resource).</summary>
    [Fact]
    public async Task ResolveAccessScope_returns_None_when_no_effective_grant()
    {
        var (_, scopeResolver, actor, action) = await _fixture.GivenRegisteredActionWithNoGrantAsync();

        var scope = await scopeResolver.ResolveAsync(actor, action, "Test.Resource");

        Assert.IsType<AccessScope.None>(scope);
    }

    /// <summary>C2: mirrors Unrestricted_grant_allows_regardless_of_resource_owner but
    /// asserts the query-side (ResolveAccessScope) contract directly, not just the
    /// single-resource (Authorize) contract.</summary>
    [Fact]
    public async Task ResolveAccessScope_returns_All_for_unrestricted_grant()
    {
        var tenantId = new TenantId(201);
        var principal = new PrincipalRef("test-idp", $"unrestricted-scope-actor-{Guid.NewGuid():N}");
        var (_, scopeResolver, actor, action) = await _fixture.GivenGrantAsync(
            tenantId, principal, "test.resource.read", "Test", "Test.Resource", relation: null);

        var scope = await scopeResolver.ResolveAsync(actor, action, "Test.Resource");

        Assert.IsType<AccessScope.All>(scope);
    }

    /// <summary>C3: `ResourceDescriptor` carries no tenant of its own (frozen minimal
    /// Contracts surface) — tenant isolation at the application layer instead comes
    /// from filtering RoleAssignments by `request.Actor.TenantId`
    /// (AccessAuthorizer.cs:30). An actor whose grant lives in tenant A must be denied
    /// when evaluated under an ActorContext carrying a different tenant, proving that
    /// filter is load-bearing rather than incidental.</summary>
    [Fact]
    public async Task Cross_tenant_actor_context_is_denied_despite_valid_grant_in_another_tenant()
    {
        var (authorizer, _, actor, action) = await _fixture.GivenOwnerRelationGrantAsync();
        var otherTenantActor = new ActorContext(new TenantId(actor.TenantId.Value + 1), actor.Principal, actor.CorrelationId);
        var resource = new ResourceDescriptor("Test.Resource", 1, actor.Principal);

        var decision = await authorizer.AuthorizeAsync(new AuthorizationRequest(otherTenantActor, action, resource));

        Assert.False(decision.IsAllowed);
        Assert.Equal("no_matching_grant", decision.ReasonCode);
        // The grant lives in a different tenant, so AccessAuthorizer's tenant filter
        // (`a.TenantId == request.Actor.TenantId`) makes the `items` query return zero
        // rows here — correctly Coarse, not Record; the resource was never reached.
        Assert.Equal(AuthorizationDenialStage.Coarse, decision.DenialStage);
    }

    /// <summary>C6a: a RoleAssignment whose ValidFrom is in the future must not
    /// authorize yet — the domain-level IsActiveAt is necessary but the evaluator
    /// (AccessAuthorizer's `a.ValidFrom &lt;= now` filter) is what actually enforces it.</summary>
    [Fact]
    public async Task Future_dated_assignment_does_not_authorize()
    {
        var tenantId = new TenantId(202);
        var principal = new PrincipalRef("test-idp", $"future-actor-{Guid.NewGuid():N}");
        var (authorizer, _, actor, action) = await _fixture.GivenGrantAsync(
            tenantId, principal, "test.resource.read", "Test", "Test.Resource", relation: null,
            validFrom: DateTimeOffset.UtcNow.AddDays(1), revokedAt: null);

        var decision = await authorizer.AuthorizeAsync(new AuthorizationRequest(
            actor, action, new ResourceDescriptor("Test.Resource", 1, null)));

        Assert.False(decision.IsAllowed);
        Assert.Equal("no_matching_grant", decision.ReasonCode);
        Assert.Equal(AuthorizationDenialStage.Coarse, decision.DenialStage);
    }

    /// <summary>C6b: an already-revoked (expired) RoleAssignment must not authorize.</summary>
    [Fact]
    public async Task Revoked_assignment_does_not_authorize()
    {
        var tenantId = new TenantId(203);
        var principal = new PrincipalRef("test-idp", $"revoked-actor-{Guid.NewGuid():N}");
        var validFrom = DateTimeOffset.UtcNow.AddDays(-2);
        var (authorizer, _, actor, action) = await _fixture.GivenGrantAsync(
            tenantId, principal, "test.resource.read", "Test", "Test.Resource", relation: null,
            validFrom: validFrom, revokedAt: validFrom.AddDays(1));

        var decision = await authorizer.AuthorizeAsync(new AuthorizationRequest(
            actor, action, new ResourceDescriptor("Test.Resource", 1, null)));

        Assert.False(decision.IsAllowed);
        Assert.Equal("no_matching_grant", decision.ReasonCode);
        Assert.Equal(AuthorizationDenialStage.Coarse, decision.DenialStage);
    }

    /// <summary>H3: distinguishes "recognized principal, registered action, zero
    /// grants" from "unrecognized principal" — both currently deny, but for different
    /// reason codes, and a caller may need to tell them apart.</summary>
    [Fact]
    public async Task Recognized_principal_with_no_grant_is_denied_with_no_matching_grant()
    {
        var (authorizer, _, actor, action) = await _fixture.GivenRegisteredActionWithNoGrantAsync();

        var decision = await authorizer.AuthorizeAsync(new AuthorizationRequest(
            actor, action, new ResourceDescriptor("Test.Resource", 1, null)));

        Assert.False(decision.IsAllowed);
        Assert.Equal("no_matching_grant", decision.ReasonCode);
        Assert.Equal(AuthorizationDenialStage.Coarse, decision.DenialStage);
    }

    /// <summary>H4: an actor holding two roles — one granting the action with an
    /// `owner` relation, another granting it unrestricted — must be allowed on a
    /// resource it does not own, because grants compose additively across roles
    /// rather than the first-evaluated role determining the outcome.</summary>
    [Fact]
    public async Task Additive_grants_across_two_roles_compose_without_replacement()
    {
        var (authorizer, _, actor, action) = await _fixture.GivenTwoRoleGrantsAsync(
            firstRelation: PermissionSetItem.OwnerRelation, secondRelation: null);

        var resource = new ResourceDescriptor("Test.Resource", 1, new PrincipalRef("test-idp", "someone-else"));
        var decision = await authorizer.AuthorizeAsync(new AuthorizationRequest(actor, action, resource));

        Assert.True(decision.IsAllowed);
        Assert.Equal("tenant_scope_grant", decision.ReasonCode);
        Assert.Equal(AuthorizationDenialStage.None, decision.DenialStage);
    }

    /// <summary>H7: a CREATE-style request has no resource id yet — the contract must
    /// still authorize against an unrestricted (tenant-scope) grant with
    /// `ResourceDescriptor.Id`/`OwnerPrincipal` both null.</summary>
    [Fact]
    public async Task Create_action_with_type_only_resource_is_allowed_by_unrestricted_grant()
    {
        var tenantId = new TenantId(204);
        var principal = new PrincipalRef("test-idp", $"create-actor-{Guid.NewGuid():N}");
        var (authorizer, _, actor, action) = await _fixture.GivenGrantAsync(
            tenantId, principal, "test.resource.create", "Test", "Test.Resource", relation: null);

        var decision = await authorizer.AuthorizeAsync(new AuthorizationRequest(
            actor, action, new ResourceDescriptor("Test.Resource", null, null)));

        Assert.True(decision.IsAllowed);
        Assert.Equal("tenant_scope_grant", decision.ReasonCode);
        Assert.Equal(AuthorizationDenialStage.None, decision.DenialStage);
    }
}
