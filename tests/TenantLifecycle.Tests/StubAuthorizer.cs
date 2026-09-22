using Contracts;

namespace TenantLifecycle.Tests;

public sealed class StubAuthorizer(AuthorizationEffect effect) : IAuthorizer
{
    public static readonly StubAuthorizer AlwaysAllow = new(AuthorizationEffect.Allow);

    public Task<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AuthorizationDecision(effect, effect == AuthorizationEffect.Allow ? "allow" : "deny", Guid.NewGuid(), 0));
}
