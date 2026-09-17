namespace Contracts;

/// <summary>The single-resource authorization contract (round 1 decision #28 — kept
/// separate from `IAccessScopeResolver`, which answers the collection/query question).</summary>
public interface IAuthorizer
{
    Task<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken = default);
}
