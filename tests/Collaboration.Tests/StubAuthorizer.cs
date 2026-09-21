using Contracts;

namespace Collaboration.Tests;

/// <summary>A fixed-answer IAuthorizer for Collaboration handler tests — Access.Application's
/// real AccessAuthorizer is already fully tested by Access.Tests; Collaboration's tests only
/// need to prove the handler calls IAuthorizer at the right point and reacts to
/// Allow/Deny correctly, not re-prove Access's own PDP logic.</summary>
public sealed class StubAuthorizer(AuthorizationEffect effect, AuthorizationDenialStage denialStage = AuthorizationDenialStage.None) : IAuthorizer
{
    public static readonly StubAuthorizer AlwaysAllow = new(AuthorizationEffect.Allow);

    /// <summary>A denial with no resource-specific fact consulted — the common case for
    /// "no grant at all" scenarios. Maps to 403 externally.</summary>
    public static readonly StubAuthorizer AlwaysDeny = new(AuthorizationEffect.Deny, AuthorizationDenialStage.Coarse);

    /// <summary>A denial where a grant for the action exists but doesn't cover this
    /// resource — maps to the same 404 shape as a genuinely missing resource (tenant
    /// non-leak rule), never 403.</summary>
    public static readonly StubAuthorizer RecordDenied = new(AuthorizationEffect.Deny, AuthorizationDenialStage.Record);

    public Task<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AuthorizationDecision(effect, effect == AuthorizationEffect.Allow ? "stub_allow" : "stub_deny", Guid.NewGuid(), 0, denialStage));
}
