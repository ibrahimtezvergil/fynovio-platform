using Contracts;

namespace CRM.Tests;

/// <summary>A fixed-answer IAuthorizer for CRM handler tests — Access.Application's
/// real AccessAuthorizer is already fully tested by Access.Tests; CRM's tests only
/// need to prove the handler calls IAuthorizer at the right point and reacts to
/// Allow/Deny correctly, not re-prove Access's own PDP logic.</summary>
public sealed class StubAuthorizer(AuthorizationEffect effect) : IAuthorizer
{
    public static readonly StubAuthorizer AlwaysAllow = new(AuthorizationEffect.Allow);
    public static readonly StubAuthorizer AlwaysDeny = new(AuthorizationEffect.Deny);

    public Task<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AuthorizationDecision(effect, effect == AuthorizationEffect.Allow ? "stub_allow" : "stub_deny", Guid.NewGuid(), 0));
}
