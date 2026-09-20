using Contracts;

namespace CRM.Tests;

/// <summary>A fixed-answer <see cref="IAuthorizedPrincipalDirectory"/> that records what CRM asked. Access's real
/// implementation and its agreement with the PDP are proven in Access.Tests; CRM's tests only need to prove it asks
/// the right question and honours the answer.</summary>
public sealed class StubPrincipalDirectory(IReadOnlyList<PrincipalDirectoryEntry> permitted) : IAuthorizedPrincipalDirectory
{
    public static StubPrincipalDirectory Nobody => new([]);

    public static StubPrincipalDirectory Permitting(params PrincipalRef[] principals) =>
        new(principals.Select(p => new PrincipalDirectoryEntry(p, p.Subject, $"{p.Subject}@test.local")).ToList());

    public List<IReadOnlyCollection<ActionKey>> AskedActions { get; } = [];
    public List<(string? Search, int Take)> Listings { get; } = [];
    public int Calls => AskedActions.Count;

    public Task<IReadOnlyList<PrincipalDirectoryEntry>> ListPermittedPrincipalsAsync(
        TenantId tenantId, IReadOnlyCollection<ActionKey> requiredActions, string? search, int take, CancellationToken cancellationToken = default)
    {
        AskedActions.Add(requiredActions);
        Listings.Add((search, take));
        return Task.FromResult(permitted);
    }

    public Task<bool> IsPrincipalPermittedAsync(
        TenantId tenantId, PrincipalRef principal, IReadOnlyCollection<ActionKey> requiredActions, CancellationToken cancellationToken = default)
    {
        AskedActions.Add(requiredActions);
        return Task.FromResult(permitted.Any(p => p.Principal == principal));
    }
}

/// <summary>Allows everything and remembers each action asked about.</summary>
public sealed class RecordingAuthorizer : IAuthorizer
{
    public List<string> Actions { get; } = [];

    public Task<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        Actions.Add(request.Action.Value);
        return StubAuthorizer.AlwaysAllow.AuthorizeAsync(request, cancellationToken);
    }
}
