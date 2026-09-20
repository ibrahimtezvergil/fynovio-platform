using Contracts;

namespace CRM.Tests;

/// <summary>A scripted <see cref="IPartyIdentityResolver"/>: the real resolver (tenant scoping, merge chain, runtime
/// role) is proven in MasterData.Tests; CRM's tests only need to prove it asks it and honours the answer.</summary>
public sealed class StubPartyIdentityResolver(Func<PartyRef, PartyRef?> resolve) : IPartyIdentityResolver
{
    public static StubPartyIdentityResolver Identity { get; } = new(partyRef => partyRef);
    public static StubPartyIdentityResolver Unknown { get; } = new(_ => null);

    public int Calls { get; private set; }

    public Task<PartyRef?> ResolveAsync(PartyRef partyRef, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(resolve(partyRef));
    }

    public Task<PartyRef?> ResolveExternalIdentityAsync(
        TenantId tenantId, string provider, string sourceInstanceRef, string? externalType, string externalId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
