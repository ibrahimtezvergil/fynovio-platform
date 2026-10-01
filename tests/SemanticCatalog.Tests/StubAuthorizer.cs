using Contracts;

namespace SemanticCatalog.Tests;

/// <summary>A fixed-answer IAuthorizer — the real PDP is proven by Access.Tests; the catalog's tests only need to prove
/// it asks the right question (see CatalogAuthorizationAgreementTests) and reacts to Allow/Deny.</summary>
public sealed class StubAuthorizer(AuthorizationEffect effect, AuthorizationDenialStage denialStage = AuthorizationDenialStage.None) : IAuthorizer
{
    public static readonly StubAuthorizer AlwaysAllow = new(AuthorizationEffect.Allow);
    public static readonly StubAuthorizer AlwaysDeny = new(AuthorizationEffect.Deny, AuthorizationDenialStage.Coarse);

    public Task<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AuthorizationDecision(effect, effect == AuthorizationEffect.Allow ? "stub_allow" : "stub_deny", Guid.NewGuid(), 0, denialStage));
}

/// <summary>Records what it was asked, so a test can assert the exact action key and resource descriptor.</summary>
public sealed class RecordingAuthorizer : IAuthorizer
{
    public List<AuthorizationRequest> Requests { get; } = [];

    public Task<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(new AuthorizationDecision(AuthorizationEffect.Allow, "recorded", Guid.NewGuid(), 0, AuthorizationDenialStage.None));
    }
}

public static class TestTenants
{
    private static long _next = 1000;

    public static TenantId Next() => new(Interlocked.Increment(ref _next));
}
